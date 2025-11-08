namespace Recam.Services.DTOs;

public record SetCoverImageResponse(
    int ListingId,
    int MediaId,
    string CoverImageUrl
);