using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Recam.DataAccess;
using Recam.Models.Entities;
using Recam.Models.Enums;
using Recam.Respostitories.Inplementations;
using Xunit;

namespace Recam.UnitTests;

public class ListingCaseRepositoryTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly RecamDbContext _db;
    private readonly ListingCaseRepository _repo;

    public ListingCaseRepositoryTests()
    {
        // 1) 持久内存库（连接不断开，库就存在）
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();

        // 2) DbContext
        var options = new DbContextOptionsBuilder<RecamDbContext>()
            .UseSqlite(_conn)
            .EnableSensitiveDataLogging()
            .Options;

        _db = new RecamDbContext(options);
        _db.Database.EnsureCreated();

        // 3) 基础数据
        Seed(_db);

        // 4) 仓储
        _repo = new ListingCaseRepository(_db);
    }

    private static ApplicationUser MakeUser(string id) => new ApplicationUser
    {
        Id = id,
        UserName = id,
        Email = $"{id}@example.com"
    };

    private static Agent MakeAgent(string id) => new Agent
    {
        Id = id,
        AgentFirstName = "A",
        AgentLastName = "B",
        CompanyName = "Co"
    };

    private static ListingCase MakeListing(int id, string ownerUserId, string title = "seed") => new ListingCase
    {
        Id = id,
        Title = title,
        Description = "desc",
        Street = $"Street-{id}",
        City = "CityX",
        State = "ST",
        PostalCode = 12345,
        Longitude = 100.123456m,
        Latitude  =  10.654321m,
        Price = 111111.11m,
        Bedrooms = 3,
        Bathrooms = 2,
        Garages = 1,
        FloorArea = 88.50m,
        PropertyType = PropertyType.House,
        SaleCategory = SaleCategory.ForSale,
        ListingCaseStatus = ListingCaseStatus.Created,
        CoverImageUrl = "https://example.com/c.jpg",
        UserId = ownerUserId,
        IsDeleted = false
    };

    private static MediaAsset MakeMedia(int id, int listingId, string uploaderUserId, bool isDeleted = false) => new MediaAsset
    {
        Id = id,
        ListingCaseId = listingId,
        MediaType = MediaType.Photo,
        MediaUrl = $"https://example.com/{id}.jpg",
        UploadedAt = DateTime.UtcNow.AddMinutes(-id),
        IsHero = false,
        IsSelected = false,
        IsDeleted = isDeleted,
        UserId = uploaderUserId
    };

    private static CaseContact MakeContact(int id, int listingId) => new CaseContact
    {
        ContactId = id,
        ListingCaseId = listingId,
        FirstName = "John",
        LastName = "Smith",
        Email = $"john{id}@ex.com",
        PhoneNumber = "12345678",
        CompanyName = "ACME",
        ProfileUrl = "https://ex.com/u"
    };

    private static void Seed(RecamDbContext db)
    {
        // Users & Agent
        var owner  = MakeUser("owner-1");
        var agentU = MakeUser("agent-user-1");
        db.Users.AddRange(owner, agentU);

        var agent = MakeAgent(agentU.Id);
        db.Agents.Add(agent);

        // Listings
        var l1 = MakeListing(1, owner.Id, "L1");
        var l2 = MakeListing(2, owner.Id, "L2");
        db.ListingCases.AddRange(l1, l2);

        // Media (都未删除)
        db.MediaAssets.AddRange(
            MakeMedia(100, 1, owner.Id),
            MakeMedia(101, 1, owner.Id),
            MakeMedia(200, 2, owner.Id)
        );

        // Contacts
        db.CaseContacts.AddRange(
            MakeContact(1, 1),
            MakeContact(2, 1),
            MakeContact(3, 2)
        );

        // Agent assignments
        db.AgentListingCases.Add(new AgentListingCase
        {
            AgentId = agent.Id,
            ListingCaseId = 1
        });

        db.SaveChanges();
    }

    [Fact]
    public async Task AddAsync_ThenSave_Persists()
    {
        var before = await _db.ListingCases.CountAsync();

        var entity = MakeListing(99, "owner-1", "new-listing");
        await _repo.AddAsync(entity);
        // Save 前不落库（Count 走 SQL）
        (await _db.ListingCases.CountAsync()).Should().Be(before);

        await _repo.SaveAsync();

        (await _db.ListingCases.CountAsync()).Should().Be(before + 1);
        var row = await _db.ListingCases.FirstAsync(x => x.Id == 99);
        row.Title.Should().Be("new-listing");
    }

    [Fact]
    public async Task GetByIdAsync_Respects_NotDeleted()
    {
        // 新增一个已删除的
        var deleted = MakeListing(3, "owner-1", "deleted-one");
        deleted.IsDeleted = true;
        _db.ListingCases.Add(deleted);
        await _db.SaveChangesAsync();

        (await _repo.GetByIdAsync(1)).Should().NotBeNull();
        (await _repo.GetByIdAsync(3)).Should().BeNull(); // 被软删的不会返回
    }

    [Fact]
    public async Task Query_AsNoTracking_FiltersDeleted()
    {
        // 再软删一个
        var l2 = await _db.ListingCases.FirstAsync(x => x.Id == 2);
        l2.IsDeleted = true;
        await _db.SaveChangesAsync();

        var list = _repo.Query().ToList();
        list.Should().OnlyContain(x => !x.IsDeleted);
        list.Select(x => x.Id).Should().Contain(1).And.NotContain(2);

        var one = list.First();
        _db.Entry(one).State.Should().Be(EntityState.Detached);
    }

    [Fact]
    public async Task Update_ThenSave_PersistsChanges()
    {
        var entity = await _repo.GetForUpdateAsync(1);
        entity!.Title = "L1-updated";
        _repo.Update(entity);
        await _repo.SaveAsync();

        var again = await _repo.GetForUpdateAsync(1);
        again!.Title.Should().Be("L1-updated");
    }

    [Fact]
    public async Task AgentAssignments_And_IsAgentAssignedAsync_Work()
    {
        var rows = await _repo.AgentAssignments().ToListAsync();
        rows.Should().ContainSingle(r => r.ListingCaseId == 1 && r.AgentId == "agent-user-1");

        var yes = await _repo.IsAgentAssignedAsync(1, "agent-user-1");
        var no  = await _repo.IsAgentAssignedAsync(2, "agent-user-1");
        yes.Should().BeTrue();
        no.Should().BeFalse();
    }

    [Fact]
    public async Task AddContactAsync_ThenSave_Persists_And_ContactExistsAsync_Works()
    {
        var c = MakeContact(99, 1);
        await _repo.AddContactAsync(c);
        await _repo.SaveAsync();

        var exists = await _repo.ContactExistsAsync(1, c.Email);
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task SoftDeleteCascadeAsync_SoftDeletes_List_And_Media_Deletes_Assignments_And_Contacts()
    {
        // 前置：listing 1 有 2 媒体、2 联系人、1 指派
        (await _db.MediaAssets.CountAsync(m => m.ListingCaseId == 1 && !m.IsDeleted)).Should().Be(2);
        (await _db.CaseContacts.CountAsync(c => c.ListingCaseId == 1)).Should().Be(2);
        (await _db.Set<AgentListingCase>().CountAsync(a => a.ListingCaseId == 1)).Should().Be(1);

        var ok = await _repo.SoftDeleteCascadeAsync(1);
        ok.Should().BeTrue();

        // listing 1 已软删（GetById 不再返回）
        (await _repo.GetByIdAsync(1)).Should().BeNull();

        // Debug 状态也可查
        var state = await _repo.GetDebugStateAsync(1);
        state.Should().NotBeNull();
        state!.Value.IsDeleted.Should().BeTrue();

        // 媒体被软删
        (await _db.MediaAssets.CountAsync(m => m.ListingCaseId == 1 && m.IsDeleted)).Should().Be(2);
        // 指派被物理删
        (await _db.Set<AgentListingCase>().CountAsync(a => a.ListingCaseId == 1)).Should().Be(0);
        // 联系人被物理删
        (await _db.CaseContacts.CountAsync(c => c.ListingCaseId == 1)).Should().Be(0);

        // 另一个 listing 不受影响
        // 另一个 listing 不受影响
        var l2After = await _repo.GetByIdAsync(2);
        l2After.Should().NotBeNull();
        // l2After!.IsDeleted.Should().BeFalse();

        // 但 Media 200 仍没被软删（它属于 listing 2）
        (await _db.MediaAssets.AnyAsync(m => m.Id == 200 && m.IsDeleted)).Should().BeFalse();

        // 不存在的 id 返回 false
        (await _repo.SoftDeleteCascadeAsync(999)).Should().BeFalse();
        // 但 Media 200 仍没被软删（它属于 listing 2）
        (await _db.MediaAssets.AnyAsync(m => m.Id == 200 && m.IsDeleted)).Should().BeFalse();

        // 不存在的 id 返回 false
        (await _repo.SoftDeleteCascadeAsync(999)).Should().BeFalse();
    }

    public void Dispose()
    {
        _db?.Dispose();
        _conn?.Dispose();
    }
}