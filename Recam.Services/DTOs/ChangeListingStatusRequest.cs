using Recam.Models.Enums;

namespace Recam.Services.DTOs;

public record ChangeListingStatusRequest(ListingCaseStatus Status, string? Reason);