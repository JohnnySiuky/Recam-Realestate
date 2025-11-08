using Recam.Models.Enums;

namespace Recam.Services.DTOs;

public record MediaItemDto(
    int Id,
    MediaType MediaType,
    string MediaUrl,
    DateTime UploadedAt,
    bool IsSelect,
    bool IsHero
);