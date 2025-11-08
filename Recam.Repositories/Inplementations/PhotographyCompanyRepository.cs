using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Recam.DataAccess;
using Recam.Models.Entities;
using Recam.Respostitories.Interfaces;

namespace Recam.Respostitories.Inplementations;

public class PhotographyCompanyRepository : IPhotographyCompanyRepository
{
    private readonly RecamDbContext _db;

    public PhotographyCompanyRepository(RecamDbContext db)
    {
        _db = db;
    }
    public Task<List<T>> GetAllAsync<T>(
        Expression<Func<PhotographyCompany, T>> selector,
        CancellationToken ct = default)
    {
        return _db.PhotographyCompanies
            .AsNoTracking()
            .Include(c => c.User)
            .Select(selector)              
            .ToListAsync(ct);
    }
    
    public Task<bool> ExistsByOwnerAsync(string ownerUserId, CancellationToken ct = default)
        => _db.PhotographyCompanies.AnyAsync(x => x.Id == ownerUserId, ct);

    public Task AddAsync(PhotographyCompany company, CancellationToken ct = default)
    {
        _db.PhotographyCompanies.Add(company);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
    
    public IQueryable<PhotographyCompany> Query() => _db.PhotographyCompanies.AsNoTracking();

    public Task<bool> RelationExistsAsync(string agentId, string companyId, CancellationToken ct = default)
        => _db.Set<AgentPhotographyCompany>()
            .AnyAsync(x => x.AgentId == agentId && x.PhotographyCompanyId == companyId, ct);

    public Task AddRelationAsync(string agentId, string companyId, CancellationToken ct = default)
    {
        _db.Set<AgentPhotographyCompany>().Add(new AgentPhotographyCompany
        {
            AgentId = agentId,
            PhotographyCompanyId = companyId
        });
        return Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
    
    public IQueryable<AgentPhotographyCompany> Links()
        => _db.Set<AgentPhotographyCompany>().AsNoTracking();
}