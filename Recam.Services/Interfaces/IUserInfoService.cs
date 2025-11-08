using Recam.Services.DTOs;

namespace Recam.Services.Interfaces;

public interface IUserInfoService
{
    Task<UserMeDto> GetMeAsync(
        string userId,
        string? emailFromToken,
        IReadOnlyCollection<string> roles,
        CancellationToken ct);
}