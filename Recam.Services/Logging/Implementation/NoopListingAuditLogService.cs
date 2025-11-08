using Recam.Services.Interfaces;

namespace Recam.Services.Logging.Implementation;

public class NoopListingAuditLogService : IListingAuditLogService
{
    public Task LogAsync(ListingPublishLog log, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}