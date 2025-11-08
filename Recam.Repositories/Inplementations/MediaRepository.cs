using Microsoft.EntityFrameworkCore;
using Recam.DataAccess;
using Recam.Models.Entities;
using Recam.Respostitories.Interfaces;

namespace Recam.Respostitories.Inplementations;

public class MediaRepository : IMediaRepository
{
    private readonly RecamDbContext _db;
    public MediaRepository(RecamDbContext db) => _db = db;
    
    
    public Task AddRangeAsync(IEnumerable<MediaAsset> entities, CancellationToken ct = default)
    {
        _db.MediaAssets.AddRange(entities);
        return Task.CompletedTask;
    }
    
    public Task<List<MediaAsset>> ListByListingIdAsync(int listingCaseId, CancellationToken ct = default)
        => _db.MediaAssets.AsNoTracking()
            .Where(m => m.ListingCaseId == listingCaseId && !m.IsDeleted)
            .OrderByDescending(m => m.IsHero).ThenByDescending(m => m.UploadedAt)
            .ToListAsync(ct);

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
    public Task<MediaAsset?> GetByIdAsync(int id, CancellationToken ct = default)
        => _db.MediaAssets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
    
    public async Task<bool> SoftDeleteAsync(int id, CancellationToken ct = default)
    {
        var m = await _db.MediaAssets.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (m is null) return false;
        if (!m.IsDeleted)
        {
            m.IsDeleted = true;
            _db.MediaAssets.Update(m);
            await _db.SaveChangesAsync(ct);
            return true;
        }
        return false; // 已經刪過
    }
    
    public IQueryable<MediaAsset> Query()
        => _db.MediaAssets.AsNoTracking();
}