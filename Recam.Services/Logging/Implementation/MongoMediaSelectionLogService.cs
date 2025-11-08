using MongoDB.Driver;
using Recam.Common.Mongo;
using Recam.Services.Logging.interfaces;

namespace Recam.Services.Logging.Implementation;

public class MongoMediaSelectionLogService : IMediaSelectionLogService
{
    private readonly IMongoCollection<MediaSelectionAuditLog> _col;

    public MongoMediaSelectionLogService(MongoSettings settings)
    {
        // 跟 MongoAuditService 的写法一样：直接 new Client，不用 IHttpClientFactory 那些
        var client = new MongoClient(settings.ConnectionString);
        var db     = client.GetDatabase(settings.Database);

        _col = db.GetCollection<MediaSelectionAuditLog>(
            settings.MediaSelectionCollection ?? "MediaSelectionHistory");
    }

    public Task LogSelectionAsync(
        int listingCaseId,
        string agentUserId,
        IEnumerable<int> mediaAssetIds,
        CancellationToken ct = default)
    {
        var doc = new MediaSelectionAuditLog
        {
            ListingCaseId     = listingCaseId.ToString(),
            AgentId           = agentUserId,
            SelectedMediaIds  = mediaAssetIds.ToArray(),
            OccurredAtUtc     = DateTime.UtcNow
        };

        return _col.InsertOneAsync(doc, cancellationToken: ct);
    }
}