using Moq;
using Microsoft.AspNetCore.Identity;
using Recam.Models.Entities;
using Recam.Respostitories.Interfaces;
using Recam.Services.Email;
using Recam.Services.DTOs;
using Recam.Services.Logging.interfaces;
using Recam.Services.Logging;
using Recam.Common.Exceptions;

// 如果你的 IEmailSender 在别的命名空间，请调整 using
using Recam.Common.Email;
using Recam.Services.Interfaces;
using Recam.UnitTests.Utils;

namespace Recam.UnitTests;

public class AgentAdminServiceTests
{
    // ---------- helpers to mock Identity ----------
    private static Mock<UserManager<ApplicationUser>> MockUserManager()
        => IdentityTestDoubles.CreateUserManager();

    private static Mock<RoleManager<IdentityRole>> MockRoleManager()
        => IdentityTestDoubles.CreateRoleManager();

    private static CreateAgentRequest NewReq() =>
        // 如果你的 DTO 是 record 主构造函数，请改成命名参数 new CreateAgentRequest(Email: "...", FirstName: ..., ...)
        new CreateAgentRequest(Email: "agent@example.com", FirstName: "Ada", LastName: "Lovelace", CompanyName: "Recam Pty Ltd");

    [Fact]
    public async Task CreateAgent_Success_FullFlow()
    {
        // arrange
        var userMgr = MockUserManager();
        var roleMgr = MockRoleManager();
        var agents  = new Mock<IAgentRepository>();
        var mail    = new Mock<IEmailSender>();
        var audit   = new Mock<IAuditLogService>();
        var req     = NewReq();

        // email 不存在
        userMgr.Setup(x => x.FindByEmailAsync(req.Email))
               .ReturnsAsync((ApplicationUser?)null);

        // 角色不存在 -> 创建成功
        roleMgr.Setup(x => x.RoleExistsAsync("Agent"))
               .ReturnsAsync(false);
        roleMgr.Setup(x => x.CreateAsync(It.Is<IdentityRole>(r => r.Name == "Agent")))
               .ReturnsAsync(IdentityResult.Success);

        // 用户创建成功（顺便在回调里给 user.Id 赋个值，便于断言）
        userMgr.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
               .Callback<ApplicationUser, string>((u, pwd) => u.Id = "user-123")
               .ReturnsAsync(IdentityResult.Success);

        // 加角色成功
        userMgr.Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Agent"))
               .ReturnsAsync(IdentityResult.Success);

        // Profile 不存在 -> 写入
        agents.Setup(x => x.ProfileExistsAsync("user-123", It.IsAny<CancellationToken>()))
              .ReturnsAsync(false);
        agents.Setup(x => x.AddAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);
        agents.Setup(x => x.SaveAsync(It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);

        // 发邮件 OK
        mail.Setup(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // 审计 OK
        audit.Setup(x => x.LogAsync(It.IsAny<AuthAuditLog>(), It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);

        var svc = new AgentAdminService(userMgr.Object, roleMgr.Object, agents.Object, mail.Object, audit.Object);

        // act
        var resp = await svc.CreateAgentAsync("admin-1", req);

        // assert
        Assert.Equal("user-123", resp.UserId);
        Assert.Equal(req.Email, resp.Email);
        Assert.False(string.IsNullOrWhiteSpace(resp.TemporaryPassword));
        Assert.True(resp.TemporaryPassword.Length >= 12);

        roleMgr.Verify(x => x.CreateAsync(It.Is<IdentityRole>(r => r.Name == "Agent")), Times.Once);
        userMgr.Verify(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Once);
        userMgr.Verify(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Agent"), Times.Once);

        agents.Verify(x => x.AddAsync(It.Is<Agent>(a => a.Id == "user-123"), It.IsAny<CancellationToken>()), Times.Once);
        agents.Verify(x => x.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);

        mail.Verify(x => x.SendAsync(It.Is<EmailMessage>(m => m.To == req.Email), It.IsAny<CancellationToken>()), Times.Once);
        audit.Verify(x => x.LogAsync(It.Is<AuthAuditLog>(l =>
            l.Event == "ADMIN_CREATE_AGENT" && l.Email == req.Email && l.UserId == "admin-1"
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAgent_EmailAlreadyExists_ThrowsBadRequest()
    {
        var userMgr = MockUserManager();
        var roleMgr = MockRoleManager();
        var agents  = new Mock<IAgentRepository>();
        var mail    = new Mock<IEmailSender>();
        var audit   = new Mock<IAuditLogService>();
        var req     = NewReq();

        userMgr.Setup(x => x.FindByEmailAsync(req.Email))
               .ReturnsAsync(new ApplicationUser{ Email = req.Email, Id = "exists" });

        var svc = new AgentAdminService(userMgr.Object, roleMgr.Object, agents.Object, mail.Object, audit.Object);

        await Assert.ThrowsAsync<BadRequestException>(() => svc.CreateAgentAsync("admin-1", req));
    }

    [Fact]
    public async Task CreateAgent_RoleCreateFails_ThrowsInvalidOperation()
    {
        var userMgr = MockUserManager();
        var roleMgr = MockRoleManager();
        var agents  = new Mock<IAgentRepository>();
        var mail    = new Mock<IEmailSender>();
        var audit   = new Mock<IAuditLogService>();
        var req     = NewReq();

        userMgr.Setup(x => x.FindByEmailAsync(req.Email)).ReturnsAsync((ApplicationUser?)null);
        roleMgr.Setup(x => x.RoleExistsAsync("Agent")).ReturnsAsync(false);
        roleMgr.Setup(x => x.CreateAsync(It.IsAny<IdentityRole>()))
               .ReturnsAsync(IdentityResult.Failed(new IdentityError{ Description = "boom" }));

        var svc = new AgentAdminService(userMgr.Object, roleMgr.Object, agents.Object, mail.Object, audit.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAgentAsync("admin-1", req));
    }

    [Fact]
    public async Task CreateAgent_UserCreateFails_ThrowsInvalidOperation()
    {
        var userMgr = MockUserManager();
        var roleMgr = MockRoleManager();
        var agents  = new Mock<IAgentRepository>();
        var mail    = new Mock<IEmailSender>();
        var audit   = new Mock<IAuditLogService>();
        var req     = NewReq();

        userMgr.Setup(x => x.FindByEmailAsync(req.Email)).ReturnsAsync((ApplicationUser?)null);
        roleMgr.Setup(x => x.RoleExistsAsync("Agent")).ReturnsAsync(true);
        userMgr.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
               .ReturnsAsync(IdentityResult.Failed(new IdentityError{ Description = "create failed" }));

        var svc = new AgentAdminService(userMgr.Object, roleMgr.Object, agents.Object, mail.Object, audit.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAgentAsync("admin-1", req));
    }

    [Fact]
    public async Task CreateAgent_AddRoleFails_ThrowsInvalidOperation()
    {
        var userMgr = MockUserManager();
        var roleMgr = MockRoleManager();
        var agents  = new Mock<IAgentRepository>();
        var mail    = new Mock<IEmailSender>();
        var audit   = new Mock<IAuditLogService>();
        var req     = NewReq();

        userMgr.Setup(x => x.FindByEmailAsync(req.Email)).ReturnsAsync((ApplicationUser?)null);
        roleMgr.Setup(x => x.RoleExistsAsync("Agent")).ReturnsAsync(true);
        userMgr.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
               .ReturnsAsync(IdentityResult.Success);
        userMgr.Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Agent"))
               .ReturnsAsync(IdentityResult.Failed(new IdentityError{ Description = "add role failed" }));

        var svc = new AgentAdminService(userMgr.Object, roleMgr.Object, agents.Object, mail.Object, audit.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAgentAsync("admin-1", req));
    }

    [Fact]
    public async Task CreateAgent_ProfileAlreadyExists_SkipsInsert_ButSendsMailAndAudit()
    {
        var userMgr = MockUserManager();
        var roleMgr = MockRoleManager();
        var agents  = new Mock<IAgentRepository>();
        var mail    = new Mock<IEmailSender>();
        var audit   = new Mock<IAuditLogService>();
        var req     = NewReq();

        userMgr.Setup(x => x.FindByEmailAsync(req.Email)).ReturnsAsync((ApplicationUser?)null);
        roleMgr.Setup(x => x.RoleExistsAsync("Agent")).ReturnsAsync(true);
        userMgr.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
               .Callback<ApplicationUser, string>((u, pwd) => u.Id = "user-999")
               .ReturnsAsync(IdentityResult.Success);
        userMgr.Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Agent"))
               .ReturnsAsync(IdentityResult.Success);

        agents.Setup(x => x.ProfileExistsAsync("user-999", It.IsAny<CancellationToken>()))
              .ReturnsAsync(true);

        mail.Setup(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        audit.Setup(x => x.LogAsync(It.IsAny<AuthAuditLog>(), It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);

        var svc = new AgentAdminService(userMgr.Object, roleMgr.Object, agents.Object, mail.Object, audit.Object);

        var resp = await svc.CreateAgentAsync("admin-1", req);

        agents.Verify(x => x.AddAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()), Times.Never);
        agents.Verify(x => x.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);

        mail.Verify(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        audit.Verify(x => x.LogAsync(It.IsAny<AuthAuditLog>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}