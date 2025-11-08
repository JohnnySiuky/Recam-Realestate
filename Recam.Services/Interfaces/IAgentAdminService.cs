using Recam.Services.DTOs;

namespace Recam.Services.Interfaces;

public interface IAgentAdminService
{
    Task<CreateAgentResponse> CreateAgentAsync(
        string adminUserId,
        CreateAgentRequest req,
        CancellationToken ct = default);
}