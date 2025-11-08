using System.Linq.Expressions;
using Recam.Models.Entities;

namespace Recam.Respostitories.Interfaces;

public interface IPhotographyCompanyRepository
{
    Task<List<T>> GetAllAsync<T>(
        Expression<Func<PhotographyCompany, T>> selector,
        CancellationToken ct = default);
    
    Task<bool> ExistsByOwnerAsync(string ownerUserId, CancellationToken ct = default);
    Task AddAsync(PhotographyCompany company, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
    
    IQueryable<PhotographyCompany> Query();
    
    Task<bool> RelationExistsAsync(string agentId, string companyId, CancellationToken ct = default);
    Task AddRelationAsync(string agentId, string companyId, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
    
    IQueryable<AgentPhotographyCompany> Links();
}