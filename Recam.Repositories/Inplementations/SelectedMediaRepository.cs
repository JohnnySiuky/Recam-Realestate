using Microsoft.EntityFrameworkCore;
using Recam.DataAccess;
using Recam.Models.Entities;
using Recam.Respostitories.Interfaces;

namespace Recam.Respostitories.Inplementations;

public class SelectedMediaRepository : ISelectedMediaRepository
{
    private readonly RecamDbContext _db;
    public SelectedMediaRepository(RecamDbContext db) => _db = db;

    public IQueryable<SelectedMedia> Query() =>
        _db.Set<SelectedMedia>()
            .AsNoTracking();

    public Task<List<SelectedMedia>> ListFinalByListingIdAsync(int listingId, CancellationToken ct = default) =>
        _db.Set<SelectedMedia>()
            .AsNoTracking()
            .Include(s => s.MediaAsset)
            .Where(s => s.ListingCaseId == listingId && s.IsFinal)
            .OrderByDescending(s => s.SelectedAt)
            .ToListAsync(ct);
    
    public Task AddRangeAsync(IEnumerable<SelectedMedia> entities, CancellationToken ct = default)
    {
        _db.SelectedMedia.AddRange(entities);
        return Task.CompletedTask;
    }

    public async Task DeleteByListingAndAgentAsync(int listingId, string agentId, CancellationToken ct = default)
    {
#if NET8_0_OR_GREATER
        await _db.SelectedMedia
            .Where(s => s.ListingCaseId == listingId && s.AgentId == agentId)
            .ExecuteDeleteAsync(ct);
#else
            var olds = await _db.SelectedMedia
                .Where(s => s.ListingCaseId == listingId && s.AgentId == agentId)
                .ToListAsync(ct);
            _db.SelectedMedia.RemoveRange(olds);
#endif
    }

    public Task SaveAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
    
    public async Task DeleteByListingAsync(int listingId, CancellationToken ct = default)
    {
#if NET8_0_OR_GREATER
        await _db.SelectedMedia
            .Where(x => x.ListingCaseId == listingId)
            .ExecuteDeleteAsync(ct);
#else
        var rows = await _db.SelectedMedia
            .Where(x => x.ListingCaseId == listingId)
            .ToListAsync(ct);

        _db.SelectedMedia.RemoveRange(rows);
#endif
    }

}