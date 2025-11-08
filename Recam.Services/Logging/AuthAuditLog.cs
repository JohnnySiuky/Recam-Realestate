namespace Recam.Services.Logging;

public class AuthAuditLog
{
    public string Event { get; set; } = "";  // LOGIN_SUCCESS / LOGIN_FAILED
    public string? UserId { get; set; }
    public string? Email { get; set; }
    public string? Reason { get; set; }      // INVALID_PASSWORD / USER_NOT_FOUND / LOCKED_OUT
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}