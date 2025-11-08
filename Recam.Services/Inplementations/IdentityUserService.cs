using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Recam.Common.Exceptions;
using Recam.Models.Entities;
using Recam.Services.Interfaces;
using Recam.Services.Logging;
using Recam.Services.Logging.interfaces;

namespace Recam.Services.Email;

public class IdentityUserService : IIdentityUserService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser>? _signIn;
    private readonly IAuditLogService _audit;
    private readonly IAuthenticationSchemeProvider _schemes;

    public IdentityUserService(
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser>? signIn,
        IAuditLogService audit,
        IAuthenticationSchemeProvider schemes)
    {
        _users = users;
        _signIn = signIn;
        _audit = audit;
        _schemes = schemes;
    }

    public Task<ApplicationUser?> FindByEmailAsync(string email) => _users.FindByEmailAsync(email);
    public Task<bool> IsInRoleAsync(ApplicationUser user, string role) => _users.IsInRoleAsync(user, role);
    
    public async Task ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId)
                   ?? throw new NotFoundException("User not found.");

        IdentityResult result;

        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            // 沒有密碼（第三方帳號）→ 直接設定新密碼
            result = await _users.AddPasswordAsync(user, newPassword);
        }
        else
        {
            // 先檢查目前密碼，錯誤就回清楚訊息
            var ok = await _users.CheckPasswordAsync(user, currentPassword);
            if (!ok)
                throw new ValidationException("PASSWORD_UPDATE_FAILED",
                    new[] { "Current password is incorrect." });

            result = await _users.ChangePasswordAsync(user, currentPassword, newPassword);
        }

        if (!result.Succeeded)
            throw new ValidationException("PASSWORD_UPDATE_FAILED",
                result.Errors.Select(e => e.Description).ToArray());

        await _users.UpdateSecurityStampAsync(user);
        await _audit.LogAsync(new AuthAuditLog { Event = "PASSWORD_CHANGED", UserId = user.Id, Email = user.Email }, ct);

        var hasCookie = (await _schemes.GetAllSchemesAsync())
            .Any(s => s.Name == IdentityConstants.ApplicationScheme);
        if (hasCookie && _signIn is not null)
        {
            await _signIn.RefreshSignInAsync(user);
        }

        
        //await _signIn.RefreshSignInAsync(user);
    }
}