using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Recam.Common.Mongo;
using Recam.Services.Interfaces;

namespace Recam.Services.Logging.Implementation;

public class ListingAuditLogService : IListingAuditLogService
{
    private readonly IMongoCollection<ListingPublishLog> _collection;

    public ListingAuditLogService(IOptions<MongoSettings> optAccessor)
    {
        var settings = optAccessor.Value;

        // 只有在 mongo.Enabled=true 的分支我们才会注册这个类，
        // 所以这里可以强校验，防止你 Enabled=true 但没填连接串
        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
            throw new ArgumentException("MongoDB connection string is missing.");
        if (string.IsNullOrWhiteSpace(settings.Database))
            throw new ArgumentException("MongoDB database name is missing.");

        var client = new MongoClient(settings.ConnectionString);
        var db     = client.GetDatabase(settings.Database);

        var colName = string.IsNullOrWhiteSpace(settings.ListingPublishCollection)
            ? "ListingPublishAudit"
            : settings.ListingPublishCollection;

        _collection = db.GetCollection<ListingPublishLog>(colName);
    }

    public Task LogAsync(ListingPublishLog log, CancellationToken ct = default)
    {
        return _collection.InsertOneAsync(log, cancellationToken: ct);
    }
}