using Recam.Models.Enums;

namespace Recam.Services.DTOs;

public record FinalSelectionResponse(
    int ListingCaseId,
    ListingCaseStatus Status,
    int Count,
    IReadOnlyList<SelectedMediaItemDto> Items
);