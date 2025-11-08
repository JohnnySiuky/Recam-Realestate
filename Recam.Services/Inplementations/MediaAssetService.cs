using System.IO.Compression;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Recam.Common.Exceptions;
using Recam.Models.Entities;
using Recam.Models.Enums;
using Recam.Respostitories.Interfaces;
using Recam.Services.DTOs;
using Recam.Services.Interfaces;

namespace Recam.Services.Email;

public class MediaAssetService : IMediaAssetService
{
    private readonly IMediaUploadService _uploader;
    private readonly IListingCaseRepository _listingRepo;
    private readonly IMediaAssetRepository _mediaRepo;
    private readonly IBlobStorageService _blob;

    public MediaAssetService(
        IMediaUploadService uploader,
        IListingCaseRepository listingRepo,
        IMediaAssetRepository mediaRepo, IBlobStorageService blob)
    {
        _uploader = uploader;
        _listingRepo = listingRepo;
        _mediaRepo = mediaRepo;
        _blob = blob;
    }

    public async Task<UploadMediaAssetsResponse> UploadAsync(
        string currentUserId,
        IReadOnlyCollection<string> roles,
        int listingCaseId,
        MediaType type,
        List<IFormFile> files,
        CancellationToken ct = default)
    {
        if (files is null || files.Count == 0)
            throw new BadRequestException("files.length must be >= 1");

        // 僅 Picture 可多檔
        if (type != MediaType.Photo && files.Count > 1)
            throw new BadRequestException("Only Picture type allows multiple uploads.");

        // 權限：Admin 可任意；PhotographyCompany 只能傳自己建立的 Listing
        var q = _listingRepo.Query().Where(l => l.Id == listingCaseId && !l.IsDeleted);

        if (roles.Contains("Admin"))
        {
            // 看全部
        }
        else if (roles.Contains("PhotographyCompany"))
        {
            q = q.Where(l => l.UserId == currentUserId);
        }
        else
        {
            throw new ForbiddenException("Only Admin or PhotographyCompany can upload media.");
        }

        var listingExists = await q.AnyAsync(ct);
        if (!listingExists) throw new NotFoundException("Listing case not found or not allowed.");

        // 上傳到 Blob
        var results = new List<(string url, string contentType, long size, string fileName)>();
        if (type == MediaType.Photo && files.Count > 1)
        {
            var uploaded = await _uploader.UploadManyAsync(listingCaseId, type, files, currentUserId, ct);
            results.AddRange(uploaded.Select(u => (u.Url, u.ContentType, u.Size, u.OriginalFileName)));
        }
        else
        {
            var u = await _uploader.UploadAsync(listingCaseId, type, files[0], currentUserId, ct);
            results.Add((u.Url, u.ContentType, u.Size, u.OriginalFileName));
        }

        // 落庫 MediaAsset
        var now = DateTime.UtcNow;
        var entities = results.Select(r => new MediaAsset
        {
            MediaType      = type,
            MediaUrl       = r.url,
            UploadedAt     = now,
            IsSelected     = false,
            IsHero         = false,
            IsDeleted      = false,
            ListingCaseId  = listingCaseId,
            UserId         = currentUserId
        }).ToList();

        await _mediaRepo.AddRangeAsync(entities, ct);
        await _mediaRepo.SaveAsync(ct);

        // 組回傳（Id 已由 EF 產出）
        var items = entities.Select(e =>
            new UploadMediaItemDto(e.Id, e.MediaType, e.MediaUrl, e.UploadedAt, results[entities.IndexOf(e)].fileName)
        ).ToList();

        return new UploadMediaAssetsResponse(listingCaseId, items.Count, items);
    }
    
    public Task<MediaAsset?> GetByIdAsync(int mediaAssetId, CancellationToken ct = default)
        => _mediaRepo.GetByIdAsync(mediaAssetId, ct);

    public async Task EnsureCanAccessAsync(
        MediaAsset asset,
        string currentUserId,
        IReadOnlyCollection<string> roles,
        CancellationToken ct = default)
    {
        if (asset is null || asset.IsDeleted)
            throw new NotFoundException("Media not found.");

        // Admin 看全部
        if (roles.Contains("Admin")) return;

        // 摄影公司：只能看自己创建的 Listing 的媒体
        if (roles.Contains("PhotographyCompany"))
        {
            var ownerId = await _listingRepo.Query()
                .Where(l => l.Id == asset.ListingCaseId)
                .Select(l => l.UserId)
                .FirstOrDefaultAsync(ct);

            if (ownerId == currentUserId) return;
        }

        // Agent：只能看分配给自己的 Listing 的媒体
        if (roles.Contains("Agent"))
        {
            var assigned = await _listingRepo.AgentAssignments()
                .AnyAsync(a => a.ListingCaseId == asset.ListingCaseId && a.AgentId == currentUserId, ct);

            if (assigned) return;
        }

        throw new ForbiddenException("You are not allowed to access this media.");
    }
    
    public async Task<(Stream Content, string ContentType, string FileName)> DownloadAsync(
        int mediaAssetId,
        string currentUserId,
        IReadOnlyCollection<string> roles,
        CancellationToken ct = default)
    {
        var asset = await _mediaRepo.GetByIdAsync(mediaAssetId, ct)
                    ?? throw new NotFoundException("Media not found.");

        await EnsureCanAccessAsync(asset, currentUserId, roles, ct);

        // 走 Blob 下载流
        return await _blob.DownloadFileAsync(asset.MediaUrl, ct);
    }

    public async Task<string> GetDownloadSasAsync(
        int mediaAssetId,
        string currentUserId,
        IReadOnlyCollection<string> roles,
        int minutes = 15,
        CancellationToken ct = default)
    {
        var asset = await _mediaRepo.GetByIdAsync(mediaAssetId, ct)
                    ?? throw new NotFoundException("Media not found.");

        await EnsureCanAccessAsync(asset, currentUserId, roles, ct);

        return _blob.GetReadOnlySasUrl(asset.MediaUrl, TimeSpan.FromMinutes(minutes));
    }
    
    public async Task<(Stream Stream, string FileName)> DownloadZipAsync(
        int listingCaseId, string currentUserId, IReadOnlyCollection<string> roles, CancellationToken ct = default)
    {
        // 權限：Admin 全看；PhotographyCompany 只能看自己建立；Agent 只能看被指派
        var q = _listingRepo.Query().Where(l => l.Id == listingCaseId && !l.IsDeleted);

        if (roles.Contains("Admin"))
        {
            // all
        }
        else if (roles.Contains("PhotographyCompany"))
        {
            q = q.Where(l => l.UserId == currentUserId);
        }
        else if (roles.Contains("Agent"))
        {
            q = q.Where(l => l.AgentListingCases.Any(a => a.AgentId == currentUserId));
        }
        else
        {
            throw new ForbiddenException("Permission denied.");
        }

        var canView = await q.AnyAsync(ct);
        if (!canView) throw new NotFoundException("Listing not found or not allowed.");

        // 取媒體清單
        var assets = await _mediaRepo.ListByListingIdAsync(listingCaseId, ct);
        if (assets.Count == 0) throw new NotFoundException("No media for this listing.");

        // 打包 ZIP（若檔案可能很大，建議改用臨時檔 FileStream）
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var a in assets)
            {
                var (content, contentType, fileName) = await _blob.DownloadFileAsync(a.MediaUrl, ct);
                var safeName = $"{a.MediaType}/{fileName}"; // 依媒體型別分資料夾
                var entry = zip.CreateEntry(safeName, CompressionLevel.Fastest);
                using var entryStream = entry.Open();
                using (content) // blob stream
                {
                    await content.CopyToAsync(entryStream, ct);
                }
            }
        }
        ms.Position = 0;
        var zipName = $"listing-{listingCaseId}-{DateTime.UtcNow:yyyyMMddHHmmss}.zip";
        return (ms, zipName);
    }
}