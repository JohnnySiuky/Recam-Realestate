using Recam.Models.Entities;

namespace Recam.Respostitories.Interfaces;

public interface IMediaRepository
{
    Task<MediaAsset?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<bool> SoftDeleteAsync(int id, CancellationToken ct = default);
    IQueryable<MediaAsset> Query();
    
    Task AddRangeAsync(IEnumerable<MediaAsset> entities, CancellationToken ct = default);
    
    Task<List<MediaAsset>> ListByListingIdAsync(int listingCaseId, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}