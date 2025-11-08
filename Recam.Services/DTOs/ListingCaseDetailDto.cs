using Recam.Models.Enums;

namespace Recam.Services.DTOs;

public record ListingCaseDetailDto(
    int Id,
    string Title,
    string? Description,
    string Street,
    string City,
    string State,
    int PostalCode,
    PropertyType PropertyType,
    SaleCategory SaleCategory,
    int Bedrooms,
    int Bathrooms,
    int Garages,
    decimal? FloorArea,
    decimal? Price,
    decimal Latitude,
    decimal Longitude,
    ListingCaseStatus Status,
    DateTime CreatedAt,
    string CreatedByUserId,
    string? CreatedByEmail,
    IReadOnlyList<MediaItemDto> Media,
    IReadOnlyList<AgentSummaryDto> Agents
);