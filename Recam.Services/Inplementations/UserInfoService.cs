using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Recam.Models.Entities;
using Recam.Respostitories.Interfaces;
using Recam.Services.DTOs;
using Recam.Services.Interfaces;

namespace Recam.Services.Email;

public class UserInfoService : IUserInfoService
{
    private readonly IListingCaseRepository _listingRepo;
    private readonly UserManager<ApplicationUser> _users;

    public UserInfoService(IListingCaseRepository listingRepo, UserManager<ApplicationUser> users)
    {
        _listingRepo = listingRepo;
        _users = users;
    }

    public async Task<UserMeDto> GetMeAsync(
        string userId,
        string? emailFromToken,
        IReadOnlyCollection<string> roles,
        CancellationToken ct)
    {
        // 盡量使用 token 的 email，沒有再查 DB（避免多一次 IO）
        var email = emailFromToken;
        if (string.IsNullOrWhiteSpace(email))
            email = (await _users.FindByIdAsync(userId))?.Email ?? string.Empty;

        var isAgent = roles.Contains("Agent");
        var isPhoto = roles.Contains("PhotographyCompany");

        var assignedIds = new List<int>();
        var createdIds  = new List<int>();

        if (isAgent)
        {
            assignedIds = await _listingRepo.AgentAssignments()
                .Where(a => a.AgentId == userId)
                .Select(a => a.ListingCaseId)
                .Distinct()
                .ToListAsync(ct);
        }

        if (isPhoto)
        {
            createdIds = await _listingRepo.Query()
                .Where(l => l.UserId == userId)
                .Select(l => l.Id)
                .Distinct()
                .ToListAsync(ct);
        }

        return new UserMeDto(
            userId,
            email ?? string.Empty,
            roles.ToList(),
            assignedIds,
            createdIds);
    }
}