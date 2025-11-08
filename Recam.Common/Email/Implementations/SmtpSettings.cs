namespace Recam.Common.Email;

public class SmtpSettings
{
    public string Host { get; set; } = default!;
    public int Port { get; set; } = 587;
    public string User { get; set; } = default!;
    public string Password { get; set; } = default!;
    public bool EnableSsl { get; set; } = true;
    public bool UseStartTls { get; set; } = true; // MailKit用
}