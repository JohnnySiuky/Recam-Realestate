using Recam.Common.Email;

namespace Recam.Services.Interfaces;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}