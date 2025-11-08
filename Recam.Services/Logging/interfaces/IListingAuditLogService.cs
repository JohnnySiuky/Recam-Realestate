using Recam.Services.Logging;

namespace Recam.Services.Interfaces;

public interface IListingAuditLogService
{
    Task LogAsync(ListingPublishLog log, CancellationToken ct = default);
}