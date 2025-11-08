using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Recam.Common.Exceptions;
using Recam.Models.Entities;
using Recam.Respostitories.Interfaces;
using Recam.Services.DTOs;
using Recam.Services.Interfaces;
using Recam.Services.Logging;
using Recam.Services.Logging.interfaces;

namespace Recam.Services.Email;

public class AgentAdminService : IAgentAdminService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole> _roles;
    private readonly IAgentRepository _agents;
    private readonly IEmailSender _mail;          // 如果你的介面叫 IEmailService，改型別即可
    private readonly IAuditLogService _audit;

    public AgentAdminService(
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole> roles,
        IAgentRepository agents,
        IEmailSender mail,
        IAuditLogService audit)
    {
        _users = users;
        _roles = roles;
        _agents = agents;
        _mail = mail;
        _audit = audit;
    }

    public async Task<CreateAgentResponse> CreateAgentAsync(
        string adminUserId,
        CreateAgentRequest req,
        CancellationToken ct = default)
    {
        // 1) email 重複檢查
        var existed = await _users.FindByEmailAsync(req.Email);
        if (existed is not null)
            throw new BadRequestException("Email already registered.");

        // 2) 確保 Agent 角色存在
        const string roleName = "Agent";
        if (!await _roles.RoleExistsAsync(roleName))
        {
            var r = await _roles.CreateAsync(new IdentityRole(roleName));
            if (!r.Succeeded)
                throw new InvalidOperationException("Create role Agent failed: " +
                    string.Join("; ", r.Errors.Select(e => e.Description)));
        }

        // 3) 建使用者 + 隨機密碼
        var pwd = GenerateTempPassword();
        var user = new ApplicationUser
        {
            Email = req.Email,
            UserName = req.Email,
            EmailConfirmed = true
        };
        var createRes = await _users.CreateAsync(user, pwd);
        if (!createRes.Succeeded)
            throw new InvalidOperationException("Create user failed: " +
                string.Join("; ", createRes.Errors.Select(e => e.Description)));

        var addRoleRes = await _users.AddToRoleAsync(user, roleName);
        if (!addRoleRes.Succeeded)
            throw new InvalidOperationException("Assign role failed: " +
                string.Join("; ", addRoleRes.Errors.Select(e => e.Description)));

        // 4) 建 Agent Profile（如果還沒有）
        if (!await _agents.ProfileExistsAsync(user.Id, ct))
        {
            await _agents.AddAsync(new Agent
            {
                Id = user.Id,
                AgentFirstName = req.FirstName ?? "",
                AgentLastName  = req.LastName  ?? "",
                CompanyName    = req.CompanyName ?? "",
                AvatarUrl      = null
            }, ct);
            await _agents.SaveAsync(ct);
        }

        // 5) 發信
        var subject = "Your Recam Agent account";

// 給使用者看的 HTML 版本
        var html = $@"
<p>Hello{(string.IsNullOrWhiteSpace(req.FirstName) ? "" : ", " + WebUtility.HtmlEncode(req.FirstName))},</p>
<p>Your agent account has been created.</p>
<p><b>Email:</b> {WebUtility.HtmlEncode(req.Email)}<br/>
<b>Temporary password:</b> {WebUtility.HtmlEncode(pwd)}</p>
<p>Please sign in and change your password.</p>";

// 多數郵件客戶端會選擇 multipart/alternative 的純文字部分作為備援
        var plain = $@"Hello{(string.IsNullOrWhiteSpace(req.FirstName) ? "" : ", " + req.FirstName)},

Your agent account has been created.

Email: {req.Email}
Temporary password: {pwd}

Please sign in and change your password.";

// 組 EmailMessage —— 僅填必要欄位，其餘可留空
        var message = new EmailMessage
        {
            To            = req.Email,
            Subject       = subject,
            HtmlBody      = html,
            PlainTextBody = plain,
            // Cc = "...",
            // Bcc = "...",
            // ReplyTo = "...",
            // Attachments = new List<EmailAttachment> { ... }
        };

        await _mail.SendAsync(message, ct);

        // 6) 審計（Mongo disabled 時會是 No-op）
        await _audit.LogAsync(new AuthAuditLog
        {
            Event = "ADMIN_CREATE_AGENT",
            UserId = adminUserId,
            Email = req.Email
        }, ct);

        return new CreateAgentResponse(user.Id, user.Email!, pwd);
    }

    private static string GenerateTempPassword(int length = 12)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@$?_-";
        var bytes = RandomNumberGenerator.GetBytes(length);
        Span<char> result = stackalloc char[length];
        for (int i = 0; i < length; i++)
            result[i] = chars[bytes[i] % chars.Length];
        return new string(result);
    }
}