using Recam.Services.DTOs;

namespace Recam.Services.Interfaces;

public interface IFinalSelectionService
{
    Task<FinalSelectionResponse> GetAsync(
        int listingId,
        string currentUserId,
        IReadOnlyCollection<string> roles,
        CancellationToken ct = default);
    
    Task SaveAgentSelectionAsync(
        int listingId,
        string agentUserId,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<int> selectedMediaIds,
        bool markFinal,
        CancellationToken ct = default);
}