using Recam.Models.Entities;

namespace Recam.Services.Interfaces;

public interface IIdentityUserService
{
    Task<ApplicationUser?> FindByEmailAsync(string email);
    Task<bool> IsInRoleAsync(ApplicationUser user, string role);
    
    Task ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        CancellationToken ct = default);
}