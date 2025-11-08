using Recam.Models.Entities;

namespace Recam.Respostitories.Interfaces;

public interface IMediaAssetRepository
{
    Task AddRangeAsync(IEnumerable<MediaAsset> entities, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
    
    // 新增：按 Id 取单个资源（下载/删除会用到）
    Task<MediaAsset?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<MediaAsset>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default);
    

    // 新增：查询（列表/分组等场景直接用 IQueryable）
    IQueryable<MediaAsset> Query();
    
    Task<int> ClearHeroAsync(int listingCaseId, CancellationToken ct = default);

    // ★ 新增：把某个媒体标记为 Hero
    Task<int> SetHeroAsync(int mediaAssetId, CancellationToken ct = default);


    // 新增：软删除（把 IsDeleted = true）
    Task<bool> SoftDeleteAsync(int id, CancellationToken ct = default);
    Task<List<MediaAsset>> ListByListingIdAsync(int listingCaseId, CancellationToken ct = default);
}