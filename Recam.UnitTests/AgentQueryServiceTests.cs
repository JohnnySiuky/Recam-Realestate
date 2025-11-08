using Microsoft.EntityFrameworkCore;
using Moq;

using Recam.DataAccess;                 // RecamDbContext
using Recam.Models.Entities;            // Agent, ApplicationUser
using Recam.Services.Email;             // AgentQueryService
using Recam.Respostitories.Interfaces;
using Recam.Services.Interfaces; // IAgentRepository

namespace Recam.UnitTests;

public class AgentQueryServiceTests
{
    private static RecamDbContext NewInMemoryDb()
    {
        var opts = new DbContextOptionsBuilder<RecamDbContext>()
            .UseInMemoryDatabase(databaseName: $"agent-q-{System.Guid.NewGuid()}")
            .Options;
        return new RecamDbContext(opts);
    }

    private static void Seed(RecamDbContext db)
    {
        var u1 = new ApplicationUser { Id = "U1", Email = "ada@demo.com",  NormalizedEmail = "ADA@DEMO.COM"  };
        var u2 = new ApplicationUser { Id = "U2", Email = "grace@demo.com",NormalizedEmail = "GRACE@DEMO.COM"};
        var u3 = new ApplicationUser { Id = "U3", Email = "linus@demo.com",NormalizedEmail = "LINUS@DEMO.COM"};

        db.Users.AddRange(u1, u2, u3);

        db.Agents.AddRange(
            new Agent { Id = "U1", AgentFirstName = "Ada",   AgentLastName = "Lovelace", CompanyName = "Recam", User = u1, AvatarUrl = null },
            new Agent { Id = "U2", AgentFirstName = "Grace", AgentLastName = "Hopper",   CompanyName = "Recam", User = u2, AvatarUrl = null },
            new Agent { Id = "U3", AgentFirstName = "Linus", AgentLastName = "Torvalds", CompanyName = "Recam", User = u3, AvatarUrl = null }
        );
        db.SaveChanges();
    }

    private static IAgentQueryService CreateServiceBackedBy(RecamDbContext db)
    {
        var repo = new Mock<IAgentRepository>();
        // 让服务拿到的是 DbSet<Agent>（它实现了 IQueryable + IAsyncEnumerable）
        repo.Setup(r => r.Query()).Returns(db.Agents);
        return new AgentQueryService(repo.Object);
    }

    [Fact]
    public async Task GetAllAsync_Returns_Sorted_Projection()
    {
        using var db = NewInMemoryDb();
        Seed(db);
        var svc = CreateServiceBackedBy(db);

        var list = await svc.GetAllAsync(CancellationToken.None);

        // 应按 LastName, FirstName 排序：Hopper -> Lovelace -> Torvalds
        Assert.Equal(3, list.Count);
        Assert.Collection(list,
            a =>
            {
                Assert.Equal("Grace", a.FirstName);
                Assert.Equal("Hopper", a.LastName);
                Assert.Equal("grace@demo.com", a.Email);
            },
            a =>
            {
                Assert.Equal("Ada", a.FirstName);
                Assert.Equal("Lovelace", a.LastName);
                Assert.Equal("ada@demo.com", a.Email);
            },
            a =>
            {
                Assert.Equal("Linus", a.FirstName);
                Assert.Equal("Torvalds", a.LastName);
                Assert.Equal("linus@demo.com", a.Email);
            });
    }

    [Fact]
    public async Task FindByEmailExactAsync_Matches_By_NormalizedEmail()
    {
        using var db = NewInMemoryDb();
        Seed(db);
        var svc = CreateServiceBackedBy(db);

        // 传小写，内部按 NormalizedEmail（大写）比较
        var dto = await svc.FindByEmailExactAsync("grace@demo.com", CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal("U2", dto!.Id);
        Assert.Equal("grace@demo.com", dto.Email);
        Assert.Equal("Hopper", dto.LastName);
    }

    [Fact]
    public async Task FindByEmailExactAsync_ReturnsNull_When_NotFound()
    {
        using var db = NewInMemoryDb();
        Seed(db);
        var svc = CreateServiceBackedBy(db);

        var dto = await svc.FindByEmailExactAsync("nobody@demo.com", CancellationToken.None);

        Assert.Null(dto);
    }
}