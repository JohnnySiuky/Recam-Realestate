using Microsoft.AspNetCore.Mvc;
using Recam.Services.Interfaces;

namespace PMS.API.Controllers;

[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly IEmailSender _email;
    public NotificationsController(IEmailSender email) => _email = email;

    [HttpPost("test")]
    public async Task<IActionResult> SendTest(CancellationToken ct)
    {
        await _email.SendAsync(new EmailMessage {
            To = "someone@example.com",
            Subject = "Hello from Recam (MailKit)",
            HtmlBody = "<b>It works!</b>",
            PlainTextBody = "It works!"
        }, ct);

        return Ok();
    }
}