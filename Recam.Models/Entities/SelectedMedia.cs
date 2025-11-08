namespace Recam.Models.Entities;

public class SelectedMedia
{
    public int Id { get; set; }

    public int ListingCaseId { get; set; }
    public ListingCase ListingCase { get; set; } = default!;

    public int MediaAssetId { get; set; }
    public MediaAsset MediaAsset { get; set; } = default!;

    public string? AgentId { get; set; } = default!;
    public Agent Agent { get; set; } = default!;

    public DateTime SelectedAt { get; set; } = DateTime.UtcNow;

    // 只取最终入选的记录
    public bool IsFinal { get; set; } = true;
}