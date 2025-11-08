using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Recam.Common.Mongo;
using Recam.Services.Logging.interfaces;

namespace Recam.Services.Logging.Implementation;

public class MongoAuditService : IAuditLogService
{
    private readonly IMongoCollection<AuthAuditLog> _auditLogs;

    public MongoAuditService(IOptions<MongoSettings> settings)
    {
        var settingsValue = settings.Value;   // MongoSetting
        var client = new MongoClient(settingsValue.ConnectionString);
        var database = client.GetDatabase(settingsValue.Database);
        _auditLogs = database.GetCollection<AuthAuditLog>(settingsValue.AuditCollection);
        
    }

    public Task LogAsync(AuthAuditLog log, CancellationToken ct = default)
    {
        return _auditLogs.InsertOneAsync(log, cancellationToken: ct);
    }
}