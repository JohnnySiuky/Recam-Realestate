using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using Recam.Common.Email;
using Recam.Services.Interfaces;

namespace Recam.Services.Email;

public class MailKitEmailSender : IEmailSender
{
    private readonly EmailSettings _settings;
    public MailKitEmailSender(IOptions<EmailSettings> settings) => _settings = settings.Value;

    public async Task SendAsync(EmailMessage m, CancellationToken ct = default)
    {
        var message = BuildMimeMessage(m);

        using var client = new SmtpClient();

        // 如果你在 DEV 想忽略自簽章 (只限本機開發！)
        // client.ServerCertificateValidationCallback = (s, c, h, e) => true;

        var secure = SecureSocketOptions.Auto; // 預設自動
        if (_settings.Smtp.UseStartTls) secure = SecureSocketOptions.StartTls;
        else if (_settings.Smtp.EnableSsl) secure = SecureSocketOptions.SslOnConnect;
        else secure = SecureSocketOptions.None;

        await client.ConnectAsync(_settings.Smtp.Host, _settings.Smtp.Port, secure, ct);

        if (!string.IsNullOrWhiteSpace(_settings.Smtp.User))
            await client.AuthenticateAsync(_settings.Smtp.User, _settings.Smtp.Password, ct);

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }

    private MimeMessage BuildMimeMessage(EmailMessage m)
    {
        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
        msg.To.Add(MailboxAddress.Parse(m.To));
        if (!string.IsNullOrWhiteSpace(m.Cc))  msg.Cc.Add(MailboxAddress.Parse(m.Cc));
        if (!string.IsNullOrWhiteSpace(m.Bcc)) msg.Bcc.Add(MailboxAddress.Parse(m.Bcc));
        if (!string.IsNullOrWhiteSpace(m.ReplyTo)) msg.ReplyTo.Add(MailboxAddress.Parse(m.ReplyTo));
        msg.Subject = m.Subject;

        var builder = new BodyBuilder
        {
            TextBody = m.PlainTextBody,
            HtmlBody = m.HtmlBody
        };

        foreach (var a in m.Attachments)
            builder.Attachments.Add(a.FileName, a.Content, ContentType.Parse(a.ContentType));

        msg.Body = builder.ToMessageBody();
        return msg;
    }
}
