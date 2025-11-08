using Recam.Models.Entities;

namespace Recam.Respostitories.Interfaces;

public interface ISelectedMediaRepository
{
    IQueryable<SelectedMedia> Query(); // AsNoTracking
    Task<List<SelectedMedia>> ListFinalByListingIdAsync(int listingId, CancellationToken ct = default);
    
    Task AddRangeAsync(IEnumerable<SelectedMedia> entities, CancellationToken ct = default);

    Task DeleteByListingAndAgentAsync(int listingId, string agentId, CancellationToken ct = default);

    Task SaveAsync(CancellationToken ct = default);
    
    // 新增的：整個 listing 的舊選片都清掉 (不分 agent)
    Task DeleteByListingAsync(
        int listingId,
        CancellationToken ct = default);
}