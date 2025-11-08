using Microsoft.EntityFrameworkCore;
using Recam.Common.Exceptions;
using Recam.Respostitories.Interfaces;
using Recam.Services.Interfaces;

namespace Recam.Services.Email;

public class MediaService : IMediaService
{
    private readonly IMediaRepository _mediaRepo;
    private readonly IListingCaseRepository _listingRepo;
    private readonly ICaseHistoryService _history; 
    private readonly IBlobStorageService _blob;

    public MediaService(
        IMediaRepository mediaRepo,
        IListingCaseRepository listingRepo,
        ICaseHistoryService history, IBlobStorageService blob)
    {
        _mediaRepo   = mediaRepo;
        _listingRepo = listingRepo;
        _history     = history;
        _blob       = blob;
    }

    public async Task<bool> DeleteAsync(int mediaId, string operatorUserId, CancellationToken ct)
    {
        // 1) 取媒体（要拿到 MediaUrl 用于删 Blob）
        var asset = await _mediaRepo.GetByIdAsync(mediaId, ct)
                    ?? throw new NotFoundException("Media not found.");

        if (asset.IsDeleted) return false;

        // 2) 删 Blob（失败不影响数据库软删；这里 swallow 掉异常）
        try { await _blob.DeleteAsync(asset.MediaUrl, ct); } catch { /* log if needed */ }

        // 3) 软删数据库记录
        var changed = await _mediaRepo.SoftDeleteAsync(mediaId, ct);

        // 4) 记录历史（Mongo 关着也没事）
        if (changed)
        {
            await _history.DeletedAsync(asset.ListingCaseId, operatorUserId,
                new { mediaId = asset.Id, mediaUrl = asset.MediaUrl }, ct);
        }

        return changed;
    }
}