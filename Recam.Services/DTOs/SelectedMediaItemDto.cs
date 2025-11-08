using Recam.Models.Enums;

namespace Recam.Services.DTOs;
public record SelectedMediaItemDto(
    int MediaAssetId,
    MediaType MediaType,
    string Url,
    bool IsHero,
    DateTime SelectedAt,
    string SelectedByAgentId
);