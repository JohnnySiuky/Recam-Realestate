namespace Recam.Services.Logging;

public class MongoAuditOptions
{
    public string ConnectionString { get; set; } = default!;
    public string DatabaseName     { get; set; } = default!;
}