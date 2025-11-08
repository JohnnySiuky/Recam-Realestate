using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Recam.DataAccess;
using Recam.Models.Entities;
using Recam.Respostitories.Inplementations;
using Recam.Respostitories.Interfaces;

namespace Recam.UnitTests;

public class AgentRepositoryTests : IDisposable
{
    private readonly RecamDbContext _db;
    private readonly IAgentRepository _repo;

    public AgentRepositoryTests()
    {
        var opts = new DbContextOptionsBuilder<RecamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new RecamDbContext(opts);

        // ✅ 补齐必填字段
        _db.Agents.AddRange(
            new Agent { Id = "a1", AgentFirstName = "Alice", AgentLastName = "Lee" },
            new Agent { Id = "a2", AgentFirstName = "Bob",   AgentLastName = "Chen" }
        );
        _db.SaveChanges();

        _repo = new AgentRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task AgentProfileExistsAsync_ReturnsTrue_WhenExists()
    {
        var ok = await _repo.AgentProfileExistsAsync("a1", CancellationToken.None);
        ok.Should().BeTrue();
    }

    [Fact]
    public async Task AgentProfileExistsAsync_ReturnsFalse_WhenNotExists()
    {
        var ok = await _repo.AgentProfileExistsAsync("nope", CancellationToken.None);
        ok.Should().BeFalse();
    }

    [Fact]
    public async Task ProfileExistsAsync_ReturnsTrue_WhenExists()
    {
        var ok = await _repo.ProfileExistsAsync("a2", CancellationToken.None);
        ok.Should().BeTrue();
    }

    [Fact]
    public async Task AddAsync_ThenSaveAsync_Persists()
    {
        // ✅ 新增时同样补齐必填字段
        await _repo.AddAsync(new Agent { Id = "a3", AgentFirstName = "Carol", AgentLastName = "Doe" }, CancellationToken.None);

        // Save 前不可见
        (await _repo.AgentProfileExistsAsync("a3", CancellationToken.None)).Should().BeFalse();

        await _repo.SaveAsync(CancellationToken.None);

        // Save 后可见
        (await _repo.AgentProfileExistsAsync("a3", CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task Query_UsesAsNoTracking()
    {
        _db.ChangeTracker.Clear();

        var list = await _repo.Query().OrderBy(a => a.Id).ToListAsync();
        list.Should().HaveCount(2).And.Contain(x => x.Id == "a1").And.Contain(x => x.Id == "a2");

        // AsNoTracking()：查询完成后不应有跟踪中的 Agent
        _db.ChangeTracker.Entries<Agent>().Should().BeEmpty();
    }
}