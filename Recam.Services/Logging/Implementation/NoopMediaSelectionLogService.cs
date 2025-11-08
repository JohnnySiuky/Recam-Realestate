using Recam.Services.Logging.interfaces;

namespace Recam.Services.Logging.Implementation;

public class NoopMediaSelectionLogService : IMediaSelectionLogService
{
    public Task LogSelectionAsync(
        int listingCaseId,
        string agentUserId,
        IEnumerable<int> mediaAssetIds,
        CancellationToken ct = default)
    {
        Console.WriteLine(
            $"[MediaSelectionLog] listing={listingCaseId}, agent={agentUserId}, media=[{string.Join(",", mediaAssetIds)}] at {DateTime.UtcNow:o}");
        // 什么都不做，直接成功
        return Task.CompletedTask;
    }
}