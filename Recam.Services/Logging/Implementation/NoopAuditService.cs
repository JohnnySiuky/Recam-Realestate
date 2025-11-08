using Recam.Services.Logging.interfaces;

namespace Recam.Services.Logging.Implementation;

public sealed class NoopAuditService : IAuditLogService
{
    public Task LogAsync(AuthAuditLog log, CancellationToken ct = default) => Task.CompletedTask;

}