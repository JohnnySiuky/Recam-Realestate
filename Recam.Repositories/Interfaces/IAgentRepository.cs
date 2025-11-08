using Recam.Models.Entities;

namespace Recam.Respostitories.Interfaces;

public interface IAgentRepository
{
    Task<bool> AgentProfileExistsAsync(string agentId, CancellationToken ct = default);
    Task<bool> ProfileExistsAsync(string agentUserId, CancellationToken ct = default);
    Task AddAsync(Agent entity, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
    IQueryable<Agent> Query();   // AsNoTracking() 。 只讀 。反正也不改
    
}