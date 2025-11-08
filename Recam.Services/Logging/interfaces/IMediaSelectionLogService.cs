namespace Recam.Services.Logging.interfaces;

public interface IMediaSelectionLogService
{
    /// <summary>
    /// 记录一次 Agent 选片动作
    /// </summary>
    Task LogSelectionAsync(
        int listingCaseId,
        string agentUserId,
        IEnumerable<int> mediaAssetIds,
        CancellationToken ct = default);
}