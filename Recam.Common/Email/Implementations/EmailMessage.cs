public record EmailAttachment(string FileName, byte[] Content, string ContentType = "application/octet-stream");

public class EmailMessage
{
    public required string To { get; init; }
    public string? Cc { get; init; }
    public string? Bcc { get; init; }
    public string? ReplyTo { get; init; }
    public required string Subject { get; init; }
    public string? PlainTextBody { get; init; }
    public string? HtmlBody { get; init; }
    public List<EmailAttachment> Attachments { get; init; } = new();
}