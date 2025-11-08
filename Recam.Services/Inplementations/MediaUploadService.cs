using Microsoft.AspNetCore.Http;
using Recam.Models.Enums;
using Recam.Services.DTOs;
using Recam.Services.Interfaces;

namespace Recam.Services.Email;

public class MediaUploadService : IMediaUploadService
{
    private readonly IBlobStorageService _blob;
    private readonly IUploadPolicy _policy;

    public MediaUploadService(IBlobStorageService blob, IUploadPolicy policy)
    {
        _blob = blob;
        _policy = policy;
    }

    public async Task<UploadResult> UploadAsync(
        int listingId,
        MediaType type,
        IFormFile file,
        string userId,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            throw new ArgumentException("Empty file.", nameof(file));

        var ext = Path.GetExtension(file.FileName);
        if (!_policy.IsExtensionAllowed(type, ext))
            throw new InvalidOperationException($"File extension '{ext}' is not allowed for {type}.");

        var max = _policy.MaxSizeBytes(type);
        if (file.Length > max)
            throw new InvalidOperationException($"File too large. Max {max / (1024 * 1024)} MB.");

        // 組 blob path：listings/{id}/{folder}/{yyyyMM}/{guid}{ext}
        var folder = _policy.FolderOf(type);
        var blobPath = $"listings/{listingId}/{folder}/{DateTime.UtcNow:yyyyMM}/{Guid.NewGuid():N}{ext}";

        await using var s = file.OpenReadStream();
        var (url, contentType, size) =
            await _blob.UploadAsync(s, file.ContentType, blobPath, ct);

        return new UploadResult(blobPath, url, contentType, size, file.FileName);
    }

    public async Task<IReadOnlyList<UploadResult>> UploadManyAsync(
        int listingId,
        MediaType type,
        IEnumerable<IFormFile> files,
        string userId,
        CancellationToken ct = default)
    {
        // 只有 Picture 允許多檔
        if (type != MediaType.Photo)
            throw new InvalidOperationException("Only Picture type supports multiple files upload.");

        var results = new List<UploadResult>();
        foreach (var f in files)
        {
            results.Add(await UploadAsync(listingId, type, f, userId, ct));
        }
        return results;
    }
}