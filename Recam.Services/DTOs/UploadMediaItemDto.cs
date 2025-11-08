using Recam.Models.Enums;

namespace Recam.Services.DTOs;

public record UploadMediaItemDto(
    int Id,
    MediaType MediaType,
    string Url,
    DateTime UploadedAt,
    string OriginalFileName
);