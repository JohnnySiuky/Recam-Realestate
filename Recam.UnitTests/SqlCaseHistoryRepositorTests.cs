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

public class SqlCaseHistoryRepositoryTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly RecamDbContext _db;
    private readonly SqlCaseHistoryRepository _repo;

    public SqlCaseHistoryRepositoryTests()
    {
        // 1) SQLite 内存库（连接不关库就一直在）
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();

        // 2) DbContext
        var options = new DbContextOptionsBuilder<RecamDbContext>()
            .UseSqlite(_conn)
            .EnableSensitiveDataLogging()
            .Options;

        _db = new RecamDbContext(options);
        _db.Database.EnsureCreated();

        // 3) 基础数据（满足 ListingCase 的必填字段与外键）
        Seed(_db);

        // 4) 仓储
        _repo = new SqlCaseHistoryRepository(_db);
    }

    private static ApplicationUser MakeUser(string id) => new ApplicationUser
    {
        Id = id,
        UserName = id,
        Email = $"{id}@example.com"
    };

    private static ListingCase MakeListing(int id, string ownerUserId) => new ListingCase
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
        Price = 888888.88m,
        Bedrooms = 3,
        Bathrooms = 2,
        Garages = 1,
        FloorArea = 90.50m,
        PropertyType = PropertyType.House,
        SaleCategory = SaleCategory.ForSale,
        ListingCaseStatus = ListingCaseStatus.Created,
        CoverImageUrl = "https://example.com/cover.jpg",
        UserId = ownerUserId,
        IsDeleted = false
    };

    private static void Seed(RecamDbContext db)
    {
        // Users（Owner、Actor）
        var owner = MakeUser("owner-1");
        var actor = MakeUser("actor-1");
        db.Users.AddRange(owner, actor);

        // ListingCase（满足 NOT NULL & 外键）
        db.ListingCases.Add(MakeListing(1, owner.Id));

        db.SaveChanges();
    }

    [Fact]
    public async Task AddAsync_DoesNotPersist_UntilSave()
    {
        var before = await _db.Set<CaseHistory>().CountAsync();

        await _repo.AddAsync(new CaseHistory
        {
            ListingCaseId = 1,
            Event = "Created",
            ActorUserId = "actor-1",
            AtUtc = DateTime.UtcNow
        });

        // 未调用 Save 前，DB 中仍旧是旧数量（Count 走 SQL，不算跟踪中的 Added 实体）
        var mid = await _db.Set<CaseHistory>().CountAsync();
        mid.Should().Be(before);

        await _repo.SaveAsync();

        var after = await _db.Set<CaseHistory>().CountAsync();
        after.Should().Be(before + 1);
    }

    [Fact]
    public async Task AddAsync_ThenSave_Persists_And_FieldsMatch()
    {
        var now = DateTime.UtcNow;

        var entity = new CaseHistory
        {
            ListingCaseId = 1,
            Event = "StatusChanged",
            ActorUserId = "actor-1",
            AtUtc = now
        };

        await _repo.AddAsync(entity);
        await _repo.SaveAsync();

        var row = await _db.Set<CaseHistory>()
            .OrderByDescending(x => x.AtUtc)
            .FirstAsync();

        row.ListingCaseId.Should().Be(1);
        row.Event.Should().Be("StatusChanged");
        row.ActorUserId.Should().Be("actor-1");
        row.AtUtc.Should().BeCloseTo(now, TimeSpan.FromSeconds(2));
    }

    public void Dispose()
    {
        _db?.Dispose();
        _conn?.Dispose();
    }
}