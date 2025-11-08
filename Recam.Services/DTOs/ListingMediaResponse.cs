namespace Recam.Services.DTOs;

public record ListingMediaResponse(
    int ListingId,
    IReadOnlyList<ListingMediaGroupDto> Groups
);