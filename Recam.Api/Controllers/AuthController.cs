using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Recam.Common.Auth;
using Recam.Common.Auth.Implementation;
using Recam.Common.Models;
using Recam.Models.Entities;
using Recam.Services.DTOs;
using Recam.Services.Interfaces;
using Recam.Services.Logging;
using Recam.Services.Logging.interfaces;
using LoginRequest = Recam.Services.DTOs.LoginRequest;

namespace PMS.API.Controllers;


[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole> _roles;
    private readonly IJwtTokenService _jwt;
    private readonly IAuditLogService _audit;
    private readonly IOptions<JwtSettings> _jwtOpts;
    private readonly IPhotographyCompanyService  _photographyCompanyService;

    public AuthController(
        SignInManager<ApplicationUser> signIn,
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole> roles,
        IJwtTokenService jwt,
        IAuditLogService audit,
        IOptions<JwtSettings> jwtOpts,
        IPhotographyCompanyService  photographyCompanyService)
    {
        _signIn = signIn;
        _users = users;
        _roles = roles;
        _jwt = jwt;
        _audit = audit;
        _jwtOpts = jwtOpts;
        _photographyCompanyService = photographyCompanyService;
    }


    public record LoginResponse(
        string Token,
        DateTime ExpiresAt,
        string UserId,
        string Email,
        IEnumerable<string> Roles);

    public record RegisterResponse(string UserId, string Email, IEnumerable<string> Roles);

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest body,
        CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();

        var user = await _users.FindByEmailAsync(body.Email);
        if (user is null)
        {
            await _audit.LogAsync(
                new AuthAuditLog
                    { Event = "LOGIN_FAILED", Email = body.Email, Reason = "USER_NOT_FOUND", Ip = ip, UserAgent = ua },
                ct);
            return Unauthorized(ApiResponse<LoginResponse>.Fail("Invalid email or password", "UNAUTHORIZED"));
        }

        var res = await _signIn.CheckPasswordSignInAsync(user, body.Password, lockoutOnFailure: true);
        if (!res.Succeeded)
        {
            await _audit.LogAsync(new AuthAuditLog
            {
                Event = "LOGIN_FAILED", Email = body.Email, UserId = user.Id,
                Reason = res.IsLockedOut ? "LOCKED_OUT" : "INVALID_PASSWORD",
                Ip = ip, UserAgent = ua
            }, ct);
            return Unauthorized(ApiResponse<LoginResponse>.Fail(
                res.IsLockedOut ? "Account locked. Try later." : "Invalid email or password",
                "UNAUTHORIZED"));
        }

        var roles = await _users.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? string.Empty),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var token = _jwt.CreateToken(claims);

        var minutes = _jwtOpts.Value.ExpiresMinute > 0 ? _jwtOpts.Value.ExpiresMinute : 60; // ★ 注意是 ExpiresMinutes
        var expires = DateTime.UtcNow.AddMinutes(minutes);

        await _audit.LogAsync(
            new AuthAuditLog { Event = "LOGIN_SUCCESS", UserId = user.Id, Email = user.Email, Ip = ip, UserAgent = ua },
            ct);

        var resp = new LoginResponse(token, expires, user.Id, user.Email ?? string.Empty, roles);
        return Ok(ApiResponse<LoginResponse>.Success(resp, "Login success"));
    }

    [AllowAnonymous]
    [HttpPost("register/photography-company")]
    [ProducesResponseType(typeof(ApiResponse<RegisterResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<RegisterResponse>>> RegisterPhotographyCompany(
        [FromBody] RegisterPhotographyCompanyRequest req,
        CancellationToken ct)
    {
        // FluentValidation 會先驗證.. passed

        var existing = await _users.FindByEmailAsync(req.Email);
        if (existing is not null)
        {
            await _audit.LogAsync(new AuthAuditLog { Event = "REGISTER_FAILED", Email = req.Email, Reason = "EMAIL_EXISTS",
                Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString()
            }, ct);
            return BadRequest(ApiResponse<object>.Fail("Email already registered", "EMAIL_EXISTS"));
        }

        // 確保角色存在
        const string roleName = "PhotographyCompany";
        if (!await _roles.RoleExistsAsync(roleName))
        {
            var roleRes = await _roles.CreateAsync(new IdentityRole(roleName));
            if (!roleRes.Succeeded)
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Fail("Failed to create role", "ROLE_CREATE_FAILED",
                        roleRes.Errors.Select(e => e.Description).ToArray()));
        }

        // 建立使用者
        var user = new ApplicationUser
        {
            UserName = req.Email,
            Email = req.Email,
            EmailConfirmed = false,
            // 如果 ApplicationUser 有 CompanyName 等欄位可在此賦值
            // CompanyName = req.CompanyName
        };

        var createRes = await _users.CreateAsync(user, req.Password);
        if (!createRes.Succeeded)
        {
            await _audit.LogAsync(new AuthAuditLog { Event = "REGISTER_FAILED", Email = req.Email, Reason = "IDENTITY_CREATE_FAILED",
                Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString()
            }, ct);

            return BadRequest(ApiResponse<object>.Fail(
                "Failed to register",
                "IDENTITY_CREATE_FAILED",
                createRes.Errors.Select(e => e.Description).ToArray()));
        }

        // 加入角色
        var addRoleRes = await _users.AddToRoleAsync(user, roleName);
        if (!addRoleRes.Succeeded)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail("Failed to assign role", "ROLE_ASSIGN_FAILED",
                    addRoleRes.Errors.Select(e => e.Description).ToArray()));
        }
        
        var companyName = string.IsNullOrWhiteSpace(req.CompanyName)
            ? (user.Email ?? "New Photography Company")
            : req.CompanyName.Trim();

        // 由 Service 透過 Repository 確保/建立公司
        await _photographyCompanyService.EnsureForOwnerAsync(user.Id, companyName, ct);
        
        await _audit.LogAsync(new AuthAuditLog { Event = "REGISTER_SUCCESS", UserId = user.Id, Email = user.Email,
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        }, ct);

        var roles = await _users.GetRolesAsync(user);
        var resp = new RegisterResponse(user.Id, user.Email ?? string.Empty, roles);
        return Created(string.Empty, ApiResponse<RegisterResponse>.Success(resp, "Registered"));
    }

}