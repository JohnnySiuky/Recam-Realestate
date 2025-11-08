using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.Http;
using Moq;
using Recam.Common.Exceptions;
using Recam.Models.Entities;
using Recam.Models.Enums;
using Recam.Respostitories.Interfaces;
using Recam.Services.Email;
using Recam.Services.Interfaces;
using Recam.UnitTests.Testing;
using Xunit;

namespace Recam.UnitTests;

public class MediaAssetServiceTests
{
    private static IReadOnlyCollection<string> Roles(params string[] rs) => rs;

    private static MediaAssetService NewService(
        out Mock<IMediaUploadService> uploader,
        out Mock<IListingCaseRepository> listingRepo,
        out Mock<IMediaAssetRepository> mediaRepo,
        out Mock<IBlobStorageService> blob)
    {
        uploader = new Mock<IMediaUploadService>(MockBehavior.Strict);
        listingRepo = new Mock<IListingCaseRepository>(MockBehavior.Strict);
        mediaRepo = new Mock<IMediaAssetRepository>(MockBehavior.Strict);
        blob = new Mock<IBlobStorageService>(MockBehavior.Strict);

        return new MediaAssetService(uploader.Object, listingRepo.Object, mediaRepo.Object, blob.Object);
    }

    // ---------------- UploadAsync: 只测不进入 _uploader 的分支 ----------------

    [Fact]
    public async Task UploadAsync_EmptyFiles_ThrowsBadRequest()
    {
        var svc = NewService(out _, out _, out _, out _);
        await Assert.ThrowsAsync<BadRequestException>(() =>
            svc.UploadAsync("u1", Roles("Admin"), 123, MediaType.Photo, new List<IFormFile>()));
    }

    [Fact]
    public async Task UploadAsync_MultipleNonPhoto_ThrowsBadRequest()
    {
        var svc = NewService(out _, out _, out _, out _);
        var files = new List<IFormFile> { Mock.Of<IFormFile>(), Mock.Of<IFormFile>() };

        await Assert.ThrowsAsync<BadRequestException>(() =>
            svc.UploadAsync("u1", Roles("Admin"), 123, MediaType.Video, files));
    }

    [Fact]
    public async Task UploadAsync_RoleNotAllowed_ThrowsForbidden()
    {
        var svc = NewService(out _, out var listingRepo, out _, out _);
        var files = new List<IFormFile> { Mock.Of<IFormFile>() };

        // 服务会在角色判断前调用 Query()，Strict 模式必须先 Setup：
        listingRepo.Setup(r => r.Query())
            .Returns(Array.Empty<ListingCase>().ToAsyncQueryable());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            svc.UploadAsync("u1", Roles("SomeOtherRole"), 123, MediaType.Photo, files));
    }

    [Fact]
    public async Task UploadAsync_Admin_ListingNotFound_ThrowsNotFound()
    {
        var svc = NewService(out _, out var listingRepo, out _, out _);
        // Admin 看全部，但 AnyAsync=false
        listingRepo.Setup(r => r.Query()).Returns(Array.Empty<ListingCase>().ToAsyncQueryable());

        var files = new List<IFormFile> { Mock.Of<IFormFile>() };

        await Assert.ThrowsAsync<NotFoundException>(() =>
            svc.UploadAsync("admin", Roles("Admin"), 999, MediaType.Photo, files));
    }

    [Fact]
    public async Task UploadAsync_PhotographyCompany_NotOwner_ThrowsNotFound()
    {
        var svc = NewService(out _, out var listingRepo, out _, out _);

        // 有这个 listing，但 owner 不是 currentUserId，且服务里会加 Filter (UserId == currentUserId)
        listingRepo.Setup(r => r.Query()).Returns(new[]
        {
            new ListingCase { Id = 123, IsDeleted = false, UserId = "other-owner" }
        }.ToAsyncQueryable());

        var files = new List<IFormFile> { Mock.Of<IFormFile>() };

        await Assert.ThrowsAsync<NotFoundException>(() =>
            svc.UploadAsync("pc-1", Roles("PhotographyCompany"), 123, MediaType.Photo, files));
    }

    // ---------------- EnsureCanAccessAsync ----------------

    [Fact]
    public async Task EnsureCanAccessAsync_NullOrDeleted_ThrowsNotFound()
    {
        var svc = NewService(out _, out _, out _, out _);

        // null
        await Assert.ThrowsAsync<NotFoundException>(() =>
            svc.EnsureCanAccessAsync(null!, "u", Roles("Admin")));

        // deleted
        var deleted = new MediaAsset { Id = 1, IsDeleted = true, ListingCaseId = 10 };
        await Assert.ThrowsAsync<NotFoundException>(() =>
            svc.EnsureCanAccessAsync(deleted, "u", Roles("Admin")));
    }

    [Fact]
    public async Task EnsureCanAccessAsync_Admin_Succeeds()
    {
        var svc = NewService(out _, out _, out _, out _);

        var a = new MediaAsset { Id = 1, ListingCaseId = 10, MediaType = MediaType.Photo, IsDeleted = false };
        await svc.EnsureCanAccessAsync(a, "admin", Roles("Admin"));
    }

    [Fact]
    public async Task EnsureCanAccessAsync_PhotographyCompany_Owner_Succeeds()
    {
        var svc = NewService(out _, out var listingRepo, out _, out _);

        var a = new MediaAsset { Id = 1, ListingCaseId = 10, IsDeleted = false };
        listingRepo.Setup(r => r.Query()).Returns(new[]
        {
            new ListingCase { Id = 10, UserId = "pc-1" }
        }.ToAsyncQueryable());

        await svc.EnsureCanAccessAsync(a, "pc-1", Roles("PhotographyCompany"));
    }

    [Fact]
    public async Task EnsureCanAccessAsync_PhotographyCompany_NotOwner_Forbidden()
    {
        var svc = NewService(out _, out var listingRepo, out _, out _);

        var a = new MediaAsset { Id = 1, ListingCaseId = 10, IsDeleted = false };
        listingRepo.Setup(r => r.Query()).Returns(new[]
        {
            new ListingCase { Id = 10, UserId = "other" }
        }.ToAsyncQueryable());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            svc.EnsureCanAccessAsync(a, "pc-1", Roles("PhotographyCompany")));
    }

    [Fact]
    public async Task EnsureCanAccessAsync_Agent_Assigned_Succeeds()
    {
        var svc = NewService(out _, out var listingRepo, out _, out _);

        var a = new MediaAsset { Id = 1, ListingCaseId = 10, IsDeleted = false };
        listingRepo.Setup(r => r.AgentAssignments()).Returns(new[]
        {
            new AgentListingCase { ListingCaseId = 10, AgentId = "agent-1" }
        }.ToAsyncQueryable());

        await svc.EnsureCanAccessAsync(a, "agent-1", Roles("Agent"));
    }

    [Fact]
    public async Task EnsureCanAccessAsync_Agent_NotAssigned_Forbidden()
    {
        var svc = NewService(out _, out var listingRepo, out _, out _);

        var a = new MediaAsset { Id = 1, ListingCaseId = 10, IsDeleted = false };
        listingRepo.Setup(r => r.AgentAssignments()).Returns(Array.Empty<AgentListingCase>().ToAsyncQueryable());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            svc.EnsureCanAccessAsync(a, "agent-1", Roles("Agent")));
    }

    // ---------------- DownloadAsync ----------------

    [Fact]
    public async Task DownloadAsync_ReturnsStream_FromBlob_AfterAccessCheck()
    {
        var svc = NewService(out _, out var listingRepo, out var mediaRepo, out var blob);

        var asset = new MediaAsset { Id = 100, ListingCaseId = 10, MediaUrl = "https://blob/a.jpg", IsDeleted = false };
        mediaRepo.Setup(r => r.GetByIdAsync(100, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(asset);

        // Admin 无需查询 listingRepo（EnsureCanAccessAsync 会直接 return）
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("file-content"));
        blob.Setup(b => b.DownloadFileAsync("https://blob/a.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync((stream, "image/jpeg", "a.jpg"));

        var (content, contentType, fileName) =
            await svc.DownloadAsync(100, "admin", Roles("Admin"));

        Assert.Equal("image/jpeg", contentType);
        Assert.Equal("a.jpg", fileName);

        using var sr = new StreamReader(content);
        Assert.Equal("file-content", await sr.ReadToEndAsync());

        mediaRepo.VerifyAll();
        blob.VerifyAll();
    }

    // ---------------- GetDownloadSasAsync ----------------

    [Fact]
    public async Task GetDownloadSasAsync_ReturnsSasUrl_AfterAccessCheck()
    {
        var svc = NewService(out _, out var listingRepo, out var mediaRepo, out var blob);

        var asset = new MediaAsset { Id = 101, ListingCaseId = 10, MediaUrl = "https://blob/v.mp4", IsDeleted = false };
        mediaRepo.Setup(r => r.GetByIdAsync(101, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(asset);

        blob.Setup(b => b.GetReadOnlySasUrl("https://blob/v.mp4", It.IsAny<TimeSpan>()))
            .Returns("https://blob/v.mp4?sas=1");

        var sas = await svc.GetDownloadSasAsync(101, "admin", Roles("Admin"), minutes: 30);
        Assert.Equal("https://blob/v.mp4?sas=1", sas);

        mediaRepo.VerifyAll();
        blob.VerifyAll();
    }

    // ---------------- DownloadZipAsync ----------------

    [Fact]
    public async Task DownloadZipAsync_Admin_Success_BuildsZip_WithFoldersAndContent()
    {
        var svc = NewService(out _, out var listingRepo, out var mediaRepo, out var blob);

        // Admin：Query() 里只要 AnyAsync 为 true
        listingRepo.Setup(r => r.Query()).Returns(new[]
        {
            new ListingCase { Id = 777, IsDeleted = false }
        }.ToAsyncQueryable());

        // 两个媒体（Photo / Video）
        var assets = new List<MediaAsset>
        {
            new() { Id = 1, ListingCaseId = 777, MediaType = MediaType.Photo, MediaUrl = "blob://p1", IsDeleted = false },
            new() { Id = 2, ListingCaseId = 777, MediaType = MediaType.Video, MediaUrl = "blob://v1", IsDeleted = false },
        };
        mediaRepo.Setup(r => r.ListByListingIdAsync(777, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(assets);

        // Blob 返回两份内容
        blob.Setup(b => b.DownloadFileAsync("blob://p1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((new MemoryStream(Encoding.UTF8.GetBytes("PHOTO-1")) as Stream, "image/jpeg", "p1.jpg"));
        blob.Setup(b => b.DownloadFileAsync("blob://v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((new MemoryStream(Encoding.UTF8.GetBytes("VIDEO-1")) as Stream, "video/mp4", "v1.mp4"));

        var (zipStream, fileName) = await svc.DownloadZipAsync(777, "admin", Roles("Admin"));
        Assert.Contains("listing-777-", fileName);
        Assert.EndsWith(".zip", fileName);

        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: false);
        // 路径按 {MediaType}/{fileName}
        var e1 = zip.GetEntry("Photo/p1.jpg");
        var e2 = zip.GetEntry("Video/v1.mp4");
        Assert.NotNull(e1);
        Assert.NotNull(e2);

        using (var rs = new StreamReader(e1!.Open()))
        {
            Assert.Equal("PHOTO-1", await rs.ReadToEndAsync());
        }
        using (var rs = new StreamReader(e2!.Open()))
        {
            Assert.Equal("VIDEO-1", await rs.ReadToEndAsync());
        }

        mediaRepo.VerifyAll();
        blob.VerifyAll();
    }

    [Fact]
    public async Task DownloadZipAsync_Admin_NoAssets_ThrowsNotFound()
    {
        var svc = NewService(out _, out var listingRepo, out var mediaRepo, out _);
        listingRepo.Setup(r => r.Query()).Returns(new[]
        {
            new ListingCase { Id = 555, IsDeleted = false }
        }.ToAsyncQueryable());

        mediaRepo.Setup(r => r.ListByListingIdAsync(555, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<MediaAsset>());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            svc.DownloadZipAsync(555, "admin", Roles("Admin")));
    }

    [Fact]
    public async Task DownloadZipAsync_Agent_NotAssigned_NotFound()
    {
        var svc = NewService(out _, out var listingRepo, out _, out _);

        // Agent 需要在 Query 里带 AnyAsync(a => AgentId == currentUserId) 的筛选
        // 这里返回的 ListingCase 没有关联该 Agent → Any=false
        listingRepo.Setup(r => r.Query()).Returns(Array.Empty<ListingCase>().ToAsyncQueryable());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            svc.DownloadZipAsync(999, "agent-1", Roles("Agent")));
    }
}