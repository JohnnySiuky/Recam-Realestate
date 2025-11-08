using Recam.Models.Enums;

namespace Recam.Services.DTOs;

public record MediaGroupDto(
    MediaType MediaType,
    IReadOnlyList<MediaItemDto> Items
);