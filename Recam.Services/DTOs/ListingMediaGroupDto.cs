using Recam.Models.Enums;

namespace Recam.Services.DTOs;

public record ListingMediaGroupDto(
    MediaType Type,
    IReadOnlyList<MediaItemDto> Items,
    int Count
);