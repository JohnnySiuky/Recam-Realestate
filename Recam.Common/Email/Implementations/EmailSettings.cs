using System.Net.Mail;

namespace Recam.Common.Email;

public class EmailSettings
{
    public string FromName { get; set; } = default!;
    public string FromAddress { get; set; } = default!;
    public SmtpSettings Smtp { get; set; } = new();
    
}