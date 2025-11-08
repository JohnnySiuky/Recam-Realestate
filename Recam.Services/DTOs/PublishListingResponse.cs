namespace Recam.Services.DTOs;

public record PublishListingResponse(
    int ListingCaseId,
    string PublicUrl
);