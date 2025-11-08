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
using Recam.Respostitories.Interfaces;
using Xunit;

namespace Recam.UnitTests;

public class MediaAssetRepositoryTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly RecamDbContext _db;
    private readonly IMediaAssetRepository _repo;

    public MediaAssetRepositoryTests()
    {
        // SQLite 内存库（支持 ExecuteUpdate/ExecuteDelete）
        _conn = new SqliteConnection("Filename=:memory:");
        _conn.Open();

        // 关键：关闭外键检查，避免需要同时播种 AspNetUsers 等父表
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
        _repo = new MediaAssetRepository(_db);
    }

    private static void SeedData(RecamDbContext db)
    {
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
            }
        );

        db.MediaAssets.Add(
            new MediaAsset
            {
                Id = 201, ListingCaseId = 2, UserId = "u2",
                MediaType = MediaType.Video, MediaUrl = "blob://b1.mp4",
                UploadedAt = now.AddMinutes(-5), IsDeleted = true, IsHero = false
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
                Id = 202, ListingCaseId = 2, UserId = "u2",
                MediaType = MediaType.Photo, MediaUrl = "blob://new1.jpg",
                UploadedAt = DateTime.UtcNow, IsDeleted = false, IsHero = false
            },
            new MediaAsset
            {
                Id = 203, ListingCaseId = 2, UserId = "u2",
                MediaType = MediaType.Photo, MediaUrl = "blob://new2.jpg",
                UploadedAt = DateTime.UtcNow.AddSeconds(1), IsDeleted = false, IsHero = false
            }
        };

        await _repo.AddRangeAsync(newOnes, CancellationToken.None);
        (await _db.MediaAssets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 202)).Should().BeNull();

        await _repo.SaveAsync(CancellationToken.None);

        (await _db.MediaAssets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 202)).Should().NotBeNull();
        (await _db.MediaAssets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 203)).Should().NotBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_AsNoTracking()
    {
        _db.ChangeTracker.Clear();
        var m = await _repo.GetByIdAsync(102, CancellationToken.None);
        m.Should().NotBeNull();
        _db.ChangeTracker.Entries<MediaAsset>().Should().BeEmpty();
    }

    [Fact]
    public void Query_FiltersDeleted_AndIsNoTracking()
    {
        _db.ChangeTracker.Clear();

        var list = _repo.Query().OrderBy(x => x.Id).ToList();
        list.Should().OnlyContain(x => x.IsDeleted == false);
        list.Select(x => x.Id).Should().BeEquivalentTo(new[] { 101, 102, 103 }, o => o.WithoutStrictOrdering());

        _db.ChangeTracker.Entries<MediaAsset>().Should().BeEmpty();
    }

    [Fact]
    public async Task SoftDeleteAsync_TogglesFlag_ReturnsTrueThenFalse()
    {
        (await _db.MediaAssets.AsNoTracking().FirstAsync(x => x.Id == 101)).IsDeleted.Should().BeFalse();

        var ok = await _repo.SoftDeleteAsync(101, CancellationToken.None);
        ok.Should().BeTrue();

        (await _db.MediaAssets.AsNoTracking().FirstAsync(x => x.Id == 101)).IsDeleted.Should().BeTrue();

        (await _repo.SoftDeleteAsync(101, CancellationToken.None)).Should().BeFalse();
        (await _repo.SoftDeleteAsync(9999, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task ListByListingIdAsync_HeroFirst_ThenUploadedAtDesc()
    {
        var list = await _repo.ListByListingIdAsync(1, CancellationToken.None);
        list.Select(x => x.Id).Should().ContainInOrder(103, 102, 101);
        list.Should().OnlyContain(x => x.ListingCaseId == 1 && x.IsDeleted == false);
    }

    [Fact]
    public async Task ClearHeroAsync_ClearsOnlyThatListing()
    {
        var m102 = await _db.MediaAssets.FirstAsync(x => x.Id == 102);
        m102.IsHero = true;
        _db.Update(m102);
        await _db.SaveChangesAsync();

        var affected = await _repo.ClearHeroAsync(1, CancellationToken.None);
        affected.Should().Be(2);

        var listing1 = await _db.MediaAssets.AsNoTracking().Where(x => x.ListingCaseId == 1).ToListAsync();
        listing1.Should().OnlyContain(x => x.IsHero == false);

        var m201 = await _db.MediaAssets.AsNoTracking().FirstAsync(x => x.Id == 201);
        m201.IsHero.Should().BeFalse();
    }

    [Fact]
    public async Task SetHeroAsync_SetsFlag_Returns1Or0()
    {
        var m101 = await _db.MediaAssets.FirstAsync(x => x.Id == 101);
        m101.IsHero = false;
        _db.Update(m101);
        await _db.SaveChangesAsync();

        (await _repo.SetHeroAsync(101, CancellationToken.None)).Should().Be(1);
        (await _db.MediaAssets.AsNoTracking().FirstAsync(x => x.Id == 101)).IsHero.Should().BeTrue();

        (await _repo.SetHeroAsync(9999, CancellationToken.None)).Should().Be(0);
    }

    [Fact]
    public async Task GetByIdsAsync_FiltersDeletedAndNonExisting()
    {
        var ids = new[] { 101, 201, 103, 9999 };
        var list = await _repo.GetByIdsAsync(ids, CancellationToken.None);
        list.Select(x => x.Id).Should().BeEquivalentTo(new[] { 101, 103 });
    }
}