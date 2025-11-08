using Microsoft.EntityFrameworkCore;
using Recam.DataAccess;
using Recam.Models.Entities;
using Recam.Models.Enums;
using Recam.Respostitories.Interfaces;

namespace Recam.Respostitories.Inplementations;

public class ListingCaseRepository : IListingCaseRepository
{
    private readonly RecamDbContext _db;

    public ListingCaseRepository(RecamDbContext db)
    {
        _db = db;
    }
    
    public Task AddAsync(ListingCase entity, CancellationToken ct = default)
    {
        _db.ListingCases.Add(entity);
        return Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
    
    public Task<ListingCase?> GetByIdAsync(int id, CancellationToken ct = default)
        => _db.ListingCases.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);

    public void Update(ListingCase entity) => _db.ListingCases.Update(entity);
    public IQueryable<ListingCase> Query()
        => _db.ListingCases.AsNoTracking().Where(x => !x.IsDeleted);

    public IQueryable<AgentListingCase> AgentAssignments()
        => _db.Set<AgentListingCase>().AsNoTracking();
    
    public async Task<bool> SoftDeleteCascadeAsync(int id, CancellationToken ct = default)
    {
        // make sure transaction same
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var entity = await _db.ListingCases.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return false;
        if (!entity.IsDeleted)
        {
            entity.IsDeleted = true;
            _db.ListingCases.Update(entity);
        }

        
#if NET8_0_OR_GREATER
        await _db.MediaAssets
            .Where(m => m.ListingCaseId == id && !m.IsDeleted)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsDeleted, true), ct);
#else
        var medias = await _db.MediaAssets
            .Where(m => m.ListingCaseId == id && !m.IsDeleted)
            .ToListAsync(ct);
        foreach (var m in medias) m.IsDeleted = true;
        _db.MediaAssets.UpdateRange(medias);
#endif

        // 連動：指派關聯（AgentListingCase）無 IsDeleted，直接刪除
#if NET8_0_OR_GREATER
        await _db.Set<AgentListingCase>()
            .Where(a => a.ListingCaseId == id)
            .ExecuteDeleteAsync(ct);
#else
        var assigns = await _db.Set<AgentListingCase>()
            .Where(a => a.ListingCaseId == id).ToListAsync(ct);
        _db.RemoveRange(assigns);
#endif

        // 連動：CaseContact（model無 IsDeleted）=> 直接刪除
#if NET8_0_OR_GREATER
        await _db.CaseContacts.Where(c => c.ListingCaseId == id).ExecuteDeleteAsync(ct);
#else
        var contacts = await _db.CaseContacts.Where(c => c.ListingCaseId == id).ToListAsync(ct);
        _db.RemoveRange(contacts);
#endif

        // TODO: 若未來有 Orders/SelectedMedia 等，這裡一併軟刪/刪除

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }
    
    public IQueryable<CaseContact> Contacts()
        => _db.CaseContacts.AsNoTracking();
    
    public Task<bool> IsAgentAssignedAsync(int listingId, string agentUserId, CancellationToken ct = default)
        => _db.Set<AgentListingCase>()
            .AnyAsync(a => a.ListingCaseId == listingId && a.AgentId == agentUserId, ct);

    public Task<bool> ContactExistsAsync(int listingId, string email, CancellationToken ct = default)
        => _db.CaseContacts
            .AnyAsync(c => c.ListingCaseId == listingId && c.Email == email, ct);

    public Task AddContactAsync(CaseContact contact, CancellationToken ct = default)
    {
        _db.CaseContacts.Add(contact);
        return Task.CompletedTask;
    }
    
    public Task<(int Id, ListingCaseStatus Status, bool IsDeleted)?> GetDebugStateAsync(int id, CancellationToken ct = default)
        => _db.ListingCases
            .AsNoTracking()
            .Where(l => l.Id == id)
            .Select(l => new ValueTuple<int, ListingCaseStatus, bool>(l.Id, l.ListingCaseStatus, l.IsDeleted))
            .Cast<(int, ListingCaseStatus, bool)?>()
            .FirstOrDefaultAsync(ct);
    
    public Task<ListingCase?> GetForUpdateAsync(int id, CancellationToken ct = default)
        => _db.ListingCases
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
}