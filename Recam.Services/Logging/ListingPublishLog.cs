namespace Recam.Services.Logging;

public class ListingPublishLog
{
    public string Event { get; set; } = "LISTING_PUBLISHED";
    public int ListingCaseId { get; set; }
    public string OperatorUserId { get; set; } = default!;
    public string? PublicUrl { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}