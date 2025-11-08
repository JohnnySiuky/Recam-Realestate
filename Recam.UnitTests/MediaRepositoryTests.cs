using System;
using System.Linq;
using System.Threading;
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

public class MediaRepositoryTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly RecamDbContext _db;
    private readonly MediaRepository _repo;

    public MediaRepositoryTests()
    {
        // 用 SQLite 内存数据库（支持 EF Core 的各种翻译）
        _conn = new SqliteConnection("Filename=:memory:");
        _conn.Open();

        // 关键：关闭外键检查，避免必须播种 AspNetUsers 等父表
        using (var cmd = _conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA foreign_keys=OFF;";
            cmd.ExecuteNonQuery();
        }

        var opts = new DbContextOptionsBuilder<RecamDbContext>()
            .UseSqlite(_conn)
            .Options;

        _db = new RecamDbContext(opts);
        _db.Database.EnsureCreated();

        SeedData(_db);

        _repo = new MediaRepository(_db);
    }

    private static void SeedData(RecamDbContext db)
    {
        // 两个 Listing，足够用
        db.ListingCases.AddRange(
            new ListingCase
            {
                Id = 1,
                UserId = "owner-1",
                IsDeleted = false,
                ListingCaseStatus = default,
                Street = "1 Test St",
                City = "Testville",
                State = "TS"
            },
            new ListingCase
            {
                Id = 2,
                UserId = "owner-2",
                IsDeleted = false,
                ListingCaseStatus = default,
                Street = "2 Foo Rd",
                City = "Bar City",
                State = "BC"
            }
        );

        var now = DateTime.UtcNow;

        // Listing 1: 三张未删（含1张Hero），再加一张已删用于 Query 覆盖
        db.MediaAssets.AddRange(
            new MediaAsset
            {
                Id = 101, ListingCaseId = 1, UserId = "u1",
                MediaType = MediaType.Photo, MediaUrl = "blob://a1.jpg",
                UploadedAt = now.AddMinutes(-2), IsDeleted = false, IsHero = false
            },
            new MediaAsset
            {
                Id = 102, ListingCaseId = 1, UserId = "u1",
                MediaType = MediaType.Photo, MediaUrl = "blob://a2.jpg",
                UploadedAt = now.AddMinutes(-1), IsDeleted = false, IsHero = false
            },
            new MediaAsset
            {
                Id = 103, ListingCaseId = 1, UserId = "u1",
                MediaType = MediaType.Photo, MediaUrl = "blob://a3.jpg",
                UploadedAt = now.AddMinutes(-3), IsDeleted = false, IsHero = true
            },
            new MediaAsset
            {
                Id = 104, ListingCaseId = 1, UserId = "u1",
                MediaType = MediaType.Photo, MediaUrl = "blob://a4-deleted.jpg",
                UploadedAt = now, IsDeleted = true, IsHero = false
            }
        );

        // Listing 2: 一删一未删
        db.MediaAssets.AddRange(
            new MediaAsset
            {
                Id = 201, ListingCaseId = 2, UserId = "u2",
                MediaType = MediaType.Video, MediaUrl = "blob://b1.mp4",
                UploadedAt = now.AddMinutes(-5), IsDeleted = true, IsHero = false
            },
            new MediaAsset
            {
                Id = 202, ListingCaseId = 2, UserId = "u2",
                MediaType = MediaType.Photo, MediaUrl = "blob://b2.jpg",
                UploadedAt = now.AddMinutes(-4), IsDeleted = false, IsHero = false
            }
        );

        db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    [Fact]
    public async Task AddRangeAsync_ThenSave_Persists()
    {
        var newOnes = new[]
        {
            new MediaAsset
            {
                Id = 301, ListingCaseId = 2, UserId = "u2",
                MediaType = MediaType.Photo, MediaUrl = "blob://new1.jpg",
                UploadedAt = DateTime.UtcNow, IsDeleted = false, IsHero = false
            },
            new MediaAsset
            {
                Id = 302, ListingCaseId = 2, UserId = "u2",
                MediaType = MediaType.Photo, MediaUrl = "blob://new2.jpg",
                UploadedAt = DateTime.UtcNow.AddSeconds(1), IsDeleted = false, IsHero = false
            }
        };

        await _repo.AddRangeAsync(newOnes, CancellationToken.None);

        // Save 前读取不到（AsNoTracking + 未保存）
        (await _db.MediaAssets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 301)).Should().BeNull();

        await _repo.SaveAsync(CancellationToken.None);

        (await _db.MediaAssets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 301)).Should().NotBeNull();
        (await _db.MediaAssets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 302)).Should().NotBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_FiltersDeleted()
    {
        var ok = await _repo.GetByIdAsync(101, CancellationToken.None);
        ok.Should().NotBeNull();

        var deleted = await _repo.GetByIdAsync(104, CancellationToken.None);
        deleted.Should().BeNull(); // 被删的不会返回
    }

    [Fact]
    public void Query_AsNoTracking_ReturnsAll_IncludingDeleted()
    {
        _db.ChangeTracker.Clear();

        var list = _repo.Query().OrderBy(x => x.Id).ToList();
        list.Select(x => x.Id).Should().Contain(new[] { 101, 102, 103, 104, 201, 202 });

        // Query 不过滤已删
        list.Any(x => x.Id == 104 && x.IsDeleted).Should().BeTrue();
        list.Any(x => x.Id == 201 && x.IsDeleted).Should().BeTrue();

        // 验证 AsNoTracking
        _db.ChangeTracker.Entries<MediaAsset>().Should().BeEmpty();
    }

    [Fact]
    public async Task ListByListingIdAsync_OrdersAndFilters()
    {
        // Listing 1：应按 IsHero(desc) + UploadedAt(desc) 排序，并过滤已删(104)
        var list = await _repo.ListByListingIdAsync(1, CancellationToken.None);
        list.Should().OnlyContain(m => m.ListingCaseId == 1 && !m.IsDeleted);

        // 预期顺序：Hero(103) 优先，然后 102(较新) 再 101
        list.Select(x => x.Id).Should().ContainInOrder(103, 102, 101);
    }

    [Fact]
    public async Task SoftDeleteAsync_ReturnsTrueOnce_ThenFalse_And_GetByIdBecomesNull()
    {
        // 初始未删
        (await _db.MediaAssets.AsNoTracking().FirstAsync(x => x.Id == 102)).IsDeleted.Should().BeFalse();

        var ok1 = await _repo.SoftDeleteAsync(102, CancellationToken.None);
        ok1.Should().BeTrue();

        (await _db.MediaAssets.AsNoTracking().FirstAsync(x => x.Id == 102)).IsDeleted.Should().BeTrue();

        // 重复删除 => false
        var ok2 = await _repo.SoftDeleteAsync(102, CancellationToken.None);
        ok2.Should().BeFalse();

        // 已删后 GetByIdAsync 不再返回
        (await _repo.GetByIdAsync(102, CancellationToken.None)).Should().BeNull();

        // 不存在 => false
        (await _repo.SoftDeleteAsync(9999, CancellationToken.None)).Should().BeFalse();
    }
}