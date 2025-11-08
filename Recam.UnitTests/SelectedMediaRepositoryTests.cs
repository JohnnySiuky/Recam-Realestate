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

public class SelectedMediaRepositoryTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly RecamDbContext _db;
    private readonly SelectedMediaRepository _repo;

    public SelectedMediaRepositoryTests()
    {
        // 1) 打开 SQLite 内存库连接（连接不关，库就一直在）
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();

        // 2) 用该连接构建 DbContext
        var options = new DbContextOptionsBuilder<RecamDbContext>()
            .UseSqlite(_conn)
            .EnableSensitiveDataLogging()
            .Options;

        _db = new RecamDbContext(options);
        _db.Database.EnsureCreated();

        // 3) 基础数据
        Seed(_db);

        // 4) 仓储
        _repo = new SelectedMediaRepository(_db);
    }

    // ---------- 工厂：一次性把所有必填字段/外键补齐 ----------
    private static ApplicationUser MakeUser(string id, string? name = null, string? email = null)
        => new ApplicationUser
        {
            Id = id,
            UserName = name ?? id,
            Email = email ?? $"{id}@example.com"
        };

    private static Agent MakeAgent(string id, string first, string last)
        => new Agent
        {
            Id = id,                 // 注意：Agent.Id = ApplicationUser.Id
            AgentFirstName = first,
            AgentLastName  = last
        };

    private static ListingCase MakeListing(int id, string ownerUserId)
        => new ListingCase
        {
            Id = id,
            Title = $"listing-{id}",
            Description = "seed",
            Street = $"Street-{id}",
            City = "TestCity",
            State = "TS",
            PostalCode = 12345,
            Longitude = 100.123456m,
            Latitude  =  10.654321m,
            Price = 1234567.89m,
            Bedrooms = 3,
            Bathrooms = 2,
            Garages = 1,
            FloorArea = 88.88m,
            PropertyType = PropertyType.House,
            SaleCategory = SaleCategory.ForSale,
            ListingCaseStatus = ListingCaseStatus.Created,
            CoverImageUrl = "https://example.com/cover.jpg",
            PublicUrl = null,
            UserId = ownerUserId,    // 外键 -> AspNetUsers(Id)
            IsDeleted = false
        };

    private static MediaAsset MakeAsset(int id, int listingId, string uploaderUserId, DateTime uploadedAt, bool isHero = false)
        => new MediaAsset
        {
            Id = id,
            ListingCaseId = listingId,
            MediaType = MediaType.Photo,
            MediaUrl  = $"https://cdn.example.com/{id}.jpg",
            UploadedAt = uploadedAt,
            IsSelected = false,
            IsHero = isHero,
            IsDeleted = false,
            UserId = uploaderUserId     // 外键 -> AspNetUsers(Id)
        };

    private static void Seed(RecamDbContext db)
    {
        var now = DateTime.UtcNow;

        // ---- 先把所有会被引用到的 AspNetUsers 插进去 ----
        // 业主 / 上传者 / 各 Agent 都需要有对应的 ApplicationUser（因为有外键约束）
        var ownerUser   = MakeUser("owner-1");
        var uploader    = MakeUser("uploader-1");
        var agentUser1  = MakeUser("agent-1");
        var agentUser2  = MakeUser("agent-2");
        var agentUser3  = MakeUser("agent-3");
        db.Users.AddRange(ownerUser, uploader, agentUser1, agentUser2, agentUser3);

        // ---- 插入 Agent（Agent.Id = ApplicationUser.Id）----
        db.Agents.AddRange(
            MakeAgent("agent-1", "A1", "Last"),
            MakeAgent("agent-2", "A2", "Last"),
            MakeAgent("agent-3", "A3", "Last")
        );

        // ---- ListingCase（带所有必填字段 & 外键 UserId）----
        db.ListingCases.AddRange(
            MakeListing(1, ownerUser.Id),
            MakeListing(2, ownerUser.Id)
        );

        // ---- MediaAsset（必填 MediaUrl/UserId + 外键 ListingCaseId/UserId）----
        db.MediaAssets.AddRange(
            MakeAsset(100, 1, uploader.Id, now.AddMinutes(-30)),
            MakeAsset(101, 1, uploader.Id, now.AddMinutes(-20), isHero: true),
            MakeAsset(102, 1, uploader.Id, now.AddMinutes(-10)),
            MakeAsset(200, 2, uploader.Id, now.AddMinutes(-5))
        );

        db.SaveChanges();

        // ---- SelectedMedia：为 listing 1 放两条最终入选（不同 Agent），listing 2 放一条非最终入选 ----
        var t = DateTime.UtcNow;
        db.SelectedMedia.AddRange(
            new SelectedMedia { Id = 10, ListingCaseId = 1, MediaAssetId = 100, AgentId = "agent-1", IsFinal = true,  SelectedAt = t },
            new SelectedMedia { Id = 11, ListingCaseId = 1, MediaAssetId = 101, AgentId = "agent-2", IsFinal = true,  SelectedAt = t.AddSeconds(1) },
            new SelectedMedia { Id = 20, ListingCaseId = 2, MediaAssetId = 200, AgentId = "agent-3", IsFinal = false, SelectedAt = t.AddSeconds(2) }
        );

        db.SaveChanges();
    }

    // ------------------- 测试用例 -------------------

    [Fact]
    public async Task AddRangeAsync_ThenSave_Persists()
    {
        var t = DateTime.UtcNow;

        // 使用新 Id，避免与种子数据（10、11、20）冲突
        await _repo.AddRangeAsync(new[]
        {
            new SelectedMedia { Id = 13, ListingCaseId = 1, MediaAssetId = 100, AgentId = "agent-1", IsFinal = false, SelectedAt = t },
            new SelectedMedia { Id = 14, ListingCaseId = 1, MediaAssetId = 102, AgentId = "agent-2", IsFinal = true,  SelectedAt = t.AddSeconds(1) }
        });
        await _repo.SaveAsync();

        var ids = await _db.SelectedMedia.Select(x => x.Id).ToListAsync();
        ids.Should().Contain(new[] { 13, 14 });
    }

    [Fact]
    public async Task ListFinalByListingIdAsync_IncludesMedia_And_SortsDesc()
    {
        var result = await _repo.ListFinalByListingIdAsync(1);

        // 种子里 listing 1 有两条 IsFinal = true（Id 10、11）
        result.Should().HaveCount(2);

        // Include(MediaAsset) 生效
        result.Select(x => x.MediaAsset).Should().NotContainNulls();

        // 验证 SelectedAt 倒序
        var ordered = result.OrderByDescending(x => x.SelectedAt).Select(x => x.Id).ToArray();
        result.Select(x => x.Id).Should().Equal(ordered);
    }

    [Fact]
    public void Query_AsNoTracking()
    {
        var one = _repo.Query().First();
        _db.Entry(one).State.Should().Be(EntityState.Detached);
    }

    [Fact]
    public async Task DeleteByListingAndAgentAsync_RemovesOnlyThatAgentRows()
    {
        // 删除存在的 agent-1 在 listing 1 的数据（种子里的 Id 10）
        await _repo.DeleteByListingAndAgentAsync(1, "agent-1");
        await _repo.SaveAsync();

        var leftAgent1 = await _db.SelectedMedia.CountAsync(x => x.ListingCaseId == 1 && x.AgentId == "agent-1");
        var others     = await _db.SelectedMedia.CountAsync(x => x.ListingCaseId == 1 && x.AgentId != "agent-1");

        leftAgent1.Should().Be(0);
        others.Should().BeGreaterThan(0); // 其他 agent 仍在（例如 agent-2 的 Id 11）
    }

    [Fact]
    public async Task DeleteByListingAsync_RemovesAllForListing()
    {
        await _repo.DeleteByListingAsync(1);
        await _repo.SaveAsync();

        var count1 = await _db.SelectedMedia.CountAsync(x => x.ListingCaseId == 1);
        var count2 = await _db.SelectedMedia.CountAsync(x => x.ListingCaseId == 2);

        count1.Should().Be(0);
        count2.Should().BeGreaterThan(0); // 另一个 listing 不受影响（Id 20 仍在）
    }

    public void Dispose()
    {
        _db?.Dispose();
        _conn?.Dispose();
    }
}