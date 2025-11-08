using Microsoft.AspNetCore.Http;
using Recam.Models.Entities;
using Recam.Models.Enums;
using Recam.Services.DTOs;

namespace Recam.Services.Interfaces;

public interface IMediaAssetService
{
    Task<UploadMediaAssetsResponse> UploadAsync(
        string currentUserId,
        IReadOnlyCollection<string> roles,
        int listingCaseId,
        MediaType type,
        List<IFormFile> files,
        CancellationToken ct = default);
    
    Task<MediaAsset?> GetByIdAsync(int mediaAssetId, CancellationToken ct = default);

    // 新增：对某个资源做权限校验（Admin / 拍摄公司本人 / 被指派的 Agent）
    Task EnsureCanAccessAsync(
        MediaAsset asset,
        string currentUserId,
        IReadOnlyCollection<string> roles,
        CancellationToken ct = default);
    
    Task<(Stream Content, string ContentType, string FileName)> DownloadAsync(
        int mediaAssetId,
        string currentUserId,
        IReadOnlyCollection<string> roles,
        CancellationToken ct = default);

    // （可选）走 SAS 临时下载链接
    Task<string> GetDownloadSasAsync(
        int mediaAssetId,
        string currentUserId,
        IReadOnlyCollection<string> roles,
        int minutes = 15,
        CancellationToken ct = default);
    
    Task<(Stream Stream, string FileName)> DownloadZipAsync(
        int listingCaseId, string currentUserId, IReadOnlyCollection<string> roles,
        CancellationToken ct = default);
}