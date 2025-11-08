namespace Recam.Services.DTOs;

public record UploadMediaAssetsResponse(
    int ListingCaseId,
    int Count,
    IReadOnlyList<UploadMediaItemDto> Items
);
