using Recam.Models.Entities;
using Recam.Models.Enums;

namespace Recam.Respostitories.Interfaces;

public interface IListingCaseRepository
{
    Task AddAsync(ListingCase entity, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
    Task<ListingCase?> GetByIdAsync(int id, CancellationToken ct = default);
    void Update(ListingCase entity);
    
    IQueryable<ListingCase> Query();                // AsNoTracking
    IQueryable<AgentListingCase> AgentAssignments(); // AsNoTracking
    Task<bool> SoftDeleteCascadeAsync(int id, CancellationToken ct = default);
    IQueryable<CaseContact> Contacts();
    
    Task<bool> IsAgentAssignedAsync(int listingId, string agentUserId, CancellationToken ct = default);
    Task<bool> ContactExistsAsync(int listingId, string email, CancellationToken ct = default);
    Task AddContactAsync(CaseContact contact, CancellationToken ct = default);

    Task<(int Id, ListingCaseStatus Status, bool IsDeleted)?>
        GetDebugStateAsync(int id, CancellationToken ct = default);
    Task<ListingCase?> GetForUpdateAsync(int id, CancellationToken ct = default);
}