namespace Recam.Models.Entities;

public class CaseHistory
{
    public long Id { get; set; }
    public int ListingCaseId { get; set; }
    public string Event { get; set; } = default!;
    public string ActorUserId { get; set; } = default!;
    public DateTime AtUtc { get; set; }
    public string? PayloadJson { get; set; }
}