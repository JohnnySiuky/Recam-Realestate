using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Recam.DataAccess;
using Recam.Models.Entities;
using Recam.Respostitories.Inplementations;


namespace Recam.UnitTests;

public class PhotographyCompanyRepositoryTests : IDisposable
{
    private readonly RecamDbContext _db;
    private readonly PhotographyCompanyRepository _repo;

    public PhotographyCompanyRepositoryTests()
    {
        var opts = new DbContextOptionsBuilder<RecamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new RecamDbContext(opts);
        Seed(_db);
        _repo = new PhotographyCompanyRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    private static void Seed(RecamDbContext db)
    {
        db.PhotographyCompanies.AddRange(
            new PhotographyCompany { Id = "pc-1", PhotographyCompanyName = "One"  },
            new PhotographyCompany { Id = "pc-2", PhotographyCompanyName = "Two"  }
        );

        db.Set<AgentPhotographyCompany>().AddRange(
            new AgentPhotographyCompany { AgentId = "agent-1", PhotographyCompanyId = "pc-1" }
        );

        db.SaveChanges();
    }

    [Fact]
    public async Task AddAsync_ThenSave_Persists()
    {
        // ★★★ 新增时同样补齐必填字段
        var c = new PhotographyCompany { Id = "pc-3", PhotographyCompanyName = "Three" };
        await _repo.AddAsync(c, CancellationToken.None);

        _db.PhotographyCompanies.AsNoTracking().Any(x => x.Id == "pc-3").Should().BeFalse();

        await _repo.SaveChangesAsync(CancellationToken.None);

        var found = await _db.PhotographyCompanies.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == "pc-3");
        found.Should().NotBeNull();
        found!.Id.Should().Be("pc-3");
    }

    [Fact]
    public async Task GetAllAsync_Projection_Works_And_AsNoTracking()
    {
        _db.ChangeTracker.Clear();

        var ids = await _repo.GetAllAsync(c => c.Id, CancellationToken.None);
        ids.Should().Contain(new[] { "pc-1", "pc-2" });

        _db.ChangeTracker.Entries<PhotographyCompany>().Should().BeEmpty();
    }

    [Fact]
    public void Query_AsNoTracking_ReturnsAll()
    {
        _db.ChangeTracker.Clear();

        var list = _repo.Query().OrderBy(c => c.Id).ToList();
        list.Select(x => x.Id).Should().Contain(new[] { "pc-1", "pc-2" });

        _db.ChangeTracker.Entries<PhotographyCompany>().Should().BeEmpty();
    }

    [Fact]
    public async Task ExistsByOwnerAsync_CurrentImplementation_UsesCompanyIdEquality()
    {
        (await _repo.ExistsByOwnerAsync("pc-1", CancellationToken.None)).Should().BeTrue();
        (await _repo.ExistsByOwnerAsync("owner-1", CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task RelationExistsAsync_AddRelationAsync_And_Links_AsNoTracking()
    {
        (await _repo.RelationExistsAsync("agent-1", "pc-1", CancellationToken.None)).Should().BeTrue();
        (await _repo.RelationExistsAsync("agent-1", "pc-2", CancellationToken.None)).Should().BeFalse();

        await _repo.AddRelationAsync("agent-2", "pc-2", CancellationToken.None);
        await _repo.SaveAsync(CancellationToken.None);

        (await _repo.RelationExistsAsync("agent-2", "pc-2", CancellationToken.None)).Should().BeTrue();

        _db.ChangeTracker.Clear();
        var links = _repo.Links().OrderBy(x => x.PhotographyCompanyId).ToList();
        links.Should().ContainSingle(x => x.AgentId == "agent-1" && x.PhotographyCompanyId == "pc-1");
        links.Should().ContainSingle(x => x.AgentId == "agent-2" && x.PhotographyCompanyId == "pc-2");
        _db.ChangeTracker.Entries<AgentPhotographyCompany>().Should().BeEmpty();
    }
}