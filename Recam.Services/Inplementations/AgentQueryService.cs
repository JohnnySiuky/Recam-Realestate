using Microsoft.EntityFrameworkCore;
using Recam.Respostitories.Interfaces;
using Recam.Services.DTOs;
using Recam.Services.Interfaces;

namespace Recam.Services.Email;

public class AgentQueryService : IAgentQueryService
{
    private readonly IAgentRepository _repo;
    public AgentQueryService(IAgentRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<AgentListItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await _repo.Query()
            .OrderBy(a => a.AgentLastName)
            .ThenBy(a => a.AgentFirstName)
            .Select(a => new AgentListItemDto(
                a.Id,
                a.AgentFirstName,
                a.AgentLastName,
                a.User.Email,      // EF 會自動做 JOIN
                a.CompanyName,
                a.AvatarUrl
            ))
            .ToListAsync(ct);
    }
    
    public async Task<AgentListItemDto?> FindByEmailExactAsync(string email, CancellationToken ct = default)
    {
        var norm = email.Trim().ToUpperInvariant();
        return await _repo.Query()
            .Where(a => a.User.NormalizedEmail == norm)
            .Select(a => new AgentListItemDto(
                a.Id,
                a.AgentFirstName,
                a.AgentLastName,
                a.User.Email,
                a.CompanyName,
                a.AvatarUrl
            ))
            .FirstOrDefaultAsync(ct);
    }
}