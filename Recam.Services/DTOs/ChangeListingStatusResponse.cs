using Recam.Models.Enums;

namespace Recam.Services.DTOs;

public record ChangeListingStatusResponse(int Id, ListingCaseStatus OldStatus, ListingCaseStatus NewStatus, DateTime ChangedAt);