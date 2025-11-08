using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity;
using Moq;
using Recam.Common.Exceptions;
using Recam.Models.Entities;
using Recam.Services.DTOs;
using Recam.Services.Email;
using Recam.Services.Interfaces;
using Recam.Respostitories.Interfaces;
using Recam.UnitTests.Testing;

namespace Recam.UnitTests;

public class PhotographyCompanyServiceTests
{
    private static PhotographyCompanyService NewService(
        out Mock<IPhotographyCompanyRepository> repo,
        out Mock<IAgentRepository> agents,
        out Mock<IIdentityUserService> identity)
    {
        repo     = new Mock<IPhotographyCompanyRepository>(MockBehavior.Strict);
        agents   = new Mock<IAgentRepository>(MockBehavior.Strict);
        identity = new Mock<IIdentityUserService>(MockBehavior.Strict);

        return new PhotographyCompanyService(repo.Object, agents.Object, identity.Object);
    }

    // ---------- GetAllAsync ----------
    [Fact]
    public async Task GetAllAsync_ReturnsProjectedItems()
    {
        var svc = NewService(out var repo, out var agents, out var identity);

        var list = new List<PhotographyCompanyListItemDto>
        {
            new( "pc1", "Alpha Studio", 2 ),
            new( "pc2", "Beta Studio",  5 )
        };

        repo.Setup(r => r.GetAllAsync(
                It.IsAny<Expression<Func<PhotographyCompany, PhotographyCompanyListItemDto>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);

        var result = await svc.GetAllAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, x => x.Id == "pc1" && x.Name == "Alpha Studio" && x.AgentsCount == 2);
        Assert.Contains(result, x => x.Id == "pc2" && x.Name == "Beta Studio"  && x.AgentsCount == 5);

        repo.VerifyAll();
        agents.VerifyNoOtherCalls();
        identity.VerifyNoOtherCalls();
    }

    // ---------- EnsureForOwnerAsync ----------
    [Fact]
    public async Task EnsureForOwnerAsync_AlreadyExists_NoAdd()
    {
        var svc = NewService(out var repo, out var agents, out var identity);

        repo.Setup(r => r.ExistsByOwnerAsync("owner-1", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await svc.EnsureForOwnerAsync("owner-1", "My Co");

        repo.VerifyAll();
        repo.Verify(r => r.AddAsync(It.IsAny<PhotographyCompany>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        agents.VerifyNoOtherCalls();
        identity.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EnsureForOwnerAsync_NotExists_AddsAndSaves()
    {
        var svc = NewService(out var repo, out var agents, out var identity);

        PhotographyCompany? added = null;

        repo.Setup(r => r.ExistsByOwnerAsync("owner-2", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repo.Setup(r => r.AddAsync(It.IsAny<PhotographyCompany>(), It.IsAny<CancellationToken>()))
            .Callback<PhotographyCompany, CancellationToken>((pc, _) => added = pc)
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await svc.EnsureForOwnerAsync("owner-2", "Cool Shots");

        Assert.NotNull(added);
        Assert.Equal("owner-2", added!.Id);
        Assert.Equal("Cool Shots", added!.PhotographyCompanyName);

        repo.VerifyAll();
        agents.VerifyNoOtherCalls();
        identity.VerifyNoOtherCalls();
    }

    // ---------- AddAgentAsync ----------
    [Fact]
    public async Task AddAgentAsync_PhotographyCompany_AddsRelation_Success()
    {
        var svc = NewService(out var repo, out var agents, out var identity);

        var currentUserId = "pc-001";
        var agentUser = new ApplicationUser { Id = "agent-123", Email = "a@x.com" };

        identity.Setup(i => i.FindByEmailAsync("a@x.com")).ReturnsAsync(agentUser);
        identity.Setup(i => i.IsInRoleAsync(agentUser, "Agent")).ReturnsAsync(true);
        agents.Setup(a => a.AgentProfileExistsAsync("agent-123", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repo.Setup(r => r.RelationExistsAsync("agent-123", currentUserId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repo.Setup(r => r.AddRelationAsync("agent-123", currentUserId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        repo.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var resp = await svc.AddAgentAsync(currentUserId, new[] { "PhotographyCompany" }, "a@x.com", companyId: null, ct: default);

        Assert.Equal("agent-123", resp.AgentId);
        Assert.Equal(currentUserId, resp.CompanyId);

        identity.VerifyAll(); agents.VerifyAll(); repo.VerifyAll();
    }

    [Fact]
    public async Task AddAgentAsync_AdminMissingCompanyId_ThrowsBadRequest()
    {
        var svc = NewService(out var repo, out var agents, out var identity);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            svc.AddAgentAsync("admin", new[] { "Admin" }, "a@x.com", companyId: null, ct: default));

        repo.VerifyNoOtherCalls();
        agents.VerifyNoOtherCalls();
        identity.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AddAgentAsync_UserNotAgent_ThrowsBadRequest()
    {
        var svc = NewService(out var repo, out var agents, out var identity);

        var agentUser = new ApplicationUser { Id = "u1", Email = "a@x.com" };
        identity.Setup(i => i.FindByEmailAsync("a@x.com")).ReturnsAsync(agentUser);
        identity.Setup(i => i.IsInRoleAsync(agentUser, "Agent")).ReturnsAsync(false);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            svc.AddAgentAsync("pc-1", new[] { "PhotographyCompany" }, "a@x.com", null, default));

        identity.VerifyAll();
        agents.VerifyNoOtherCalls();
        repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AddAgentAsync_ProfileMissing_ThrowsBadRequest()
    {
        var svc = NewService(out var repo, out var agents, out var identity);

        var agentUser = new ApplicationUser { Id = "u2", Email = "b@x.com" };
        identity.Setup(i => i.FindByEmailAsync("b@x.com")).ReturnsAsync(agentUser);
        identity.Setup(i => i.IsInRoleAsync(agentUser, "Agent")).ReturnsAsync(true);
        agents.Setup(a => a.AgentProfileExistsAsync("u2", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            svc.AddAgentAsync("pc-1", new[] { "PhotographyCompany" }, "b@x.com", null, default));

        identity.VerifyAll(); agents.VerifyAll();
        repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AddAgentAsync_RelationExists_ThrowsBadRequest()
    {
        var svc = NewService(out var repo, out var agents, out var identity);

        var currentUserId = "pc-009";
        var agentUser = new ApplicationUser { Id = "ag9", Email = "c@x.com" };
        identity.Setup(i => i.FindByEmailAsync("c@x.com")).ReturnsAsync(agentUser);
        identity.Setup(i => i.IsInRoleAsync(agentUser, "Agent")).ReturnsAsync(true);
        agents.Setup(a => a.AgentProfileExistsAsync("ag9", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repo.Setup(r => r.RelationExistsAsync("ag9", currentUserId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            svc.AddAgentAsync(currentUserId, new[] { "PhotographyCompany" }, "c@x.com", null, default));

        identity.VerifyAll(); agents.VerifyAll(); repo.VerifyAll();
    }

    // ---------- GetMyAgentsAsync ----------
    [Fact]
    public async Task GetMyAgentsAsync_JoinsAndOrdersAndProjects()
    {
        var svc = NewService(out var repo, out var agents, out var identity);

        var currentPcId = "pc-100";

        repo.Setup(r => r.Links()).Returns(new[]
        {
            new AgentPhotographyCompany { AgentId = "a1", PhotographyCompanyId = currentPcId },
            new AgentPhotographyCompany { AgentId = "a2", PhotographyCompanyId = currentPcId },
            new AgentPhotographyCompany { AgentId = "other", PhotographyCompanyId = "pc-x" },
        }.ToAsyncQueryable());

        agents.Setup(a => a.Query()).Returns(new[]
        {
            new Agent { Id = "a2", AgentFirstName = "Amy",  AgentLastName = "Zed",   CompanyName = "C", AvatarUrl = "u2.png", User = new ApplicationUser { Email = "amy@x.com" } },
            new Agent { Id = "a1", AgentFirstName = "Bob",  AgentLastName = "Young", CompanyName = "B", AvatarUrl = "u1.png", User = new ApplicationUser { Email = "bob@x.com" } },
            new Agent { Id = "other", AgentFirstName = "X", AgentLastName = "A",     CompanyName = "O", AvatarUrl = "u0.png", User = new ApplicationUser { Email = "o@x.com" } },
        }.ToAsyncQueryable());

        var list = await svc.GetMyAgentsAsync(currentPcId, default);

        Assert.Equal(2, list.Count);
        // 排序：LastName, FirstName -> Young..., Zed...
        Assert.Collection(list,
            i => { Assert.Equal("a1", i.Id); Assert.Equal("bob@x.com", i.Email); },
            i => { Assert.Equal("a2", i.Id); Assert.Equal("amy@x.com", i.Email); });

        repo.VerifyAll(); agents.VerifyAll(); identity.VerifyNoOtherCalls();
    }
}