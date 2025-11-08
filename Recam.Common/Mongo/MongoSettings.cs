namespace Recam.Common.Mongo;

public class MongoSettings
{
    public bool Enabled { get; set; } = true;
    public string ConnectionString { get; set; } = default!;
    public string Database { get; set; } = default!;
    public string AuditCollection { get; set; } = "AuthAudit";
    
    public string MediaSelectionCollection { get; set; } = "MediaSelectionHistory";
    public string ListingPublishCollection { get; set; } = "ListingPublishAudit";
}
