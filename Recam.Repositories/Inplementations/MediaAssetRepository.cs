using Microsoft.EntityFrameworkCore;
using Recam.DataAccess;
using Recam.Models.Entities;
using Recam.Respostitories.Interfaces;

namespace Recam.Respostitories.Inplementations;

public class MediaAssetRepository : IMediaAssetRepository
{
    private readonly RecamDbContext _db;
    public MediaAssetRepository(RecamDbContext db) => _db = db;

    public Task AddRangeAsync(IEnumerable<MediaAsset> entities, CancellationToken ct = default)
    {
        _db.MediaAssets.AddRange(entities);
        return Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
    
    public Task<MediaAsset?> GetByIdAsync(int id, CancellationToken ct = default)
        => _db.MediaAssets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public IQueryable<MediaAsset> Query()
        => _db.MediaAssets
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

    public async Task<bool> SoftDeleteAsync(int id, CancellationToken ct = default)
    {
#if NET8_0_OR_GREATER
        var rows = await _db.MediaAssets
            .Where(x => x.Id == id && !x.IsDeleted)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsDeleted, true), ct);
        return rows > 0;
#else
        var entity = await _db.MediaAssets.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (entity == null) return false;
        entity.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        return true;
#endif
    }
    
    public Task<List<MediaAsset>> ListByListingIdAsync(int listingCaseId, CancellationToken ct = default)
        => _db.MediaAssets.AsNoTracking()
            .Where(m => m.ListingCaseId == listingCaseId && !m.IsDeleted)
            .OrderByDescending(m => m.IsHero).ThenByDescending(m => m.UploadedAt)
            .ToListAsync(ct);
    
    
    public async Task<int> ClearHeroAsync(int listingCaseId, CancellationToken ct = default)
    {
#if NET8_0_OR_GREATER
        return await _db.MediaAssets
            .Where(m => m.ListingCaseId == listingCaseId && m.IsHero)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsHero, false), ct);
#else
        var heroes = await _db.MediaAssets.Where(m => m.ListingCaseId == listingCaseId && m.IsHero).ToListAsync(ct);
        heroes.ForEach(h => h.IsHero = false);
        _db.MediaAssets.UpdateRange(heroes);
        return heroes.Count;
#endif
    }

    public async Task<int> SetHeroAsync(int mediaAssetId, CancellationToken ct = default)
    {
#if NET8_0_OR_GREATER
        return await _db.MediaAssets
            .Where(m => m.Id == mediaAssetId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsHero, true), ct);
#else
        var m = await _db.MediaAssets.FirstOrDefaultAsync(x => x.Id == mediaAssetId, ct);
        if (m is null) return 0;
        m.IsHero = true;
        _db.MediaAssets.Update(m);
        return 1;
#endif
    }
    
    public Task<List<MediaAsset>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        var set = ids.ToArray();
        return _db.MediaAssets
            .AsNoTracking()
            .Where(m => set.Contains(m.Id) && !m.IsDeleted)
            .ToListAsync(ct);
    }

}