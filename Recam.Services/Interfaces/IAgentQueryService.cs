using Recam.Services.DTOs;

namespace Recam.Services.Interfaces;

public interface IAgentQueryService
{
    Task<IReadOnlyList<AgentListItemDto>> GetAllAsync(CancellationToken ct = default);
    
    Task<AgentListItemDto?> FindByEmailExactAsync(string email, CancellationToken ct = default);
}