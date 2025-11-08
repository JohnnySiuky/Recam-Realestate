using Microsoft.AspNetCore.Http;
using Recam.Models.Enums;
using Recam.Services.DTOs;

namespace Recam.Services.Interfaces;

public interface IMediaUploadService
{
    Task<UploadResult> UploadAsync(
        int listingId,
        MediaType type,
        IFormFile file,
        string userId,
        CancellationToken ct = default);

    Task<IReadOnlyList<UploadResult>> UploadManyAsync(
        int listingId,
        MediaType type,
        IEnumerable<IFormFile> files,
        string userId,
        CancellationToken ct = default);
}