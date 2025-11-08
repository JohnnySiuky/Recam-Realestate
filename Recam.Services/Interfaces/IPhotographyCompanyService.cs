using Recam.Services.DTOs;

namespace Recam.Services.Interfaces;

public interface IPhotographyCompanyService
{
    public Task<IReadOnlyList<PhotographyCompanyListItemDto>> GetAllAsync(CancellationToken ct = default);
    Task EnsureForOwnerAsync(string ownerUserId, string name, CancellationToken ct = default);
    
    Task<AddAgentResponse> AddAgentAsync(
        string currentUserId,
        IReadOnlyCollection<string> roles,
        string agentEmail,
        string? companyId,
        CancellationToken ct);
    
    Task<IReadOnlyList<AgentListItemDto>> GetMyAgentsAsync(
        string currentUserId,
        CancellationToken ct = default);

}