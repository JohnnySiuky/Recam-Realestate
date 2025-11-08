namespace Recam.Services.Logging;

public class MediaSelectionAuditLog
{
    public string ListingCaseId { get; set; } = default!;   // 存成 string
    public string AgentId { get; set; } = default!;
    public IReadOnlyList<int> SelectedMediaIds { get; set; } = Array.Empty<int>();
    public DateTime OccurredAtUtc { get; set; }             // 记录时间
}