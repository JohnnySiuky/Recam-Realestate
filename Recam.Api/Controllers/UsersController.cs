using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recam.Common.Models;
using Recam.Services.DTOs;
using Recam.Services.Interfaces;

namespace PMS.API.Controllers;

[ApiController]
[Route("api/users")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly IUserInfoService _svc;
    private readonly IIdentityUserService _identitySvc;
    public UsersController(IUserInfoService svc, IIdentityUserService identitiySvc)
    {
        _svc = svc;
        _identitySvc = identitiySvc;
    }

    // GET /api/users/me
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<UserMeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<UserMeDto>>> GetMe(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var email  = User.FindFirstValue(ClaimTypes.Email);
        var roles  = User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray();

        var dto = await _svc.GetMeAsync(userId, email, roles, ct);
        return Ok(ApiResponse<UserMeDto>.Success(dto, "ok"));
    }
    
    [Authorize] // 任何已登入使用者
    [HttpPatch("me/password")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> ChangeMyPassword(
        [FromBody] ChangePasswordRequest req,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        await _identitySvc.ChangePasswordAsync(userId, req.CurrentPassword, req.NewPassword, ct);

        // 提示前端：舊 JWT 不會自動失效，建議用戶重新登入
        return Ok(ApiResponse<object>.Success(new
        {
            shouldReLogin = true
        }, "Password updated. Please sign in again on other devices if needed."));
    }
}