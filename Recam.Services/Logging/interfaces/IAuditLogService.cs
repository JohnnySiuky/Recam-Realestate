namespace Recam.Services.Logging.interfaces;

public interface IAuditLogService
{
    Task LogAsync(AuthAuditLog log, CancellationToken ct = default);
}