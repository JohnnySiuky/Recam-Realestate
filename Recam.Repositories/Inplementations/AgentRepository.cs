using Microsoft.EntityFrameworkCore;
using Recam.DataAccess;
using Recam.Models.Entities;
using Recam.Respostitories.Interfaces;

namespace Recam.Respostitories.Inplementations;

public class AgentRepository : IAgentRepository
{
    private readonly RecamDbContext _db;
    public AgentRepository(RecamDbContext db) => _db = db;

    public Task<bool> AgentProfileExistsAsync(string agentId, CancellationToken ct = default)
        => _db.Agents.AsNoTracking().AnyAsync(a => a.Id == agentId, ct);
    
    public Task<bool> ProfileExistsAsync(string agentUserId, CancellationToken ct = default)
        => _db.Agents.AsNoTracking().AnyAsync(a => a.Id == agentUserId, ct);

    public Task AddAsync(Agent entity, CancellationToken ct = default)
    {
        _db.Agents.Add(entity);
        return Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public IQueryable<Agent> Query()
        => _db.Agents.AsNoTracking();
}