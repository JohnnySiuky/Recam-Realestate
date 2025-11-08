using Moq;
using Xunit;
using Microsoft.AspNetCore.Http;
using Recam.Models.Enums;
using Recam.Services.DTOs;
using Recam.Services.Email;
using Recam.Services.Interfaces;

namespace Recam.UnitTests;

public class MediaUploadServiceTests
{
    private static IFormFile FakeFile(string fileName, string contentType, byte[] bytes)
    {
        var ms = new MemoryStream(bytes);
        var mock = new Mock<IFormFile>();
        mock.Setup(f => f.FileName).Returns(fileName);
        mock.Setup(f => f.ContentType).Returns(contentType);
        mock.Setup(f => f.Length).Returns(ms.Length);
        mock.Setup(f => f.OpenReadStream()).Returns(ms);
        return mock.Object;
    }

    private static MediaUploadService NewService(
        out Mock<IBlobStorageService> blob,
        out Mock<IUploadPolicy> policy)
    {
        blob = new Mock<IBlobStorageService>(MockBehavior.Strict);
        policy = new Mock<IUploadPolicy>(MockBehavior.Strict);
        return new MediaUploadService(blob.Object, policy.Object);
    }

    [Fact]
    public async Task UploadAsync_EmptyFile_Throws()
    {
        var svc = NewService(out var blob, out var policy);
        var file = FakeFile("a.jpg", "image/jpeg", Array.Empty<byte>());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.UploadAsync(1, MediaType.Photo, file, "u1"));

        blob.VerifyNoOtherCalls();
        policy.VerifyNoOtherCalls(); // 长度为 0，政策不会被调用
    }

    [Fact]
    public async Task UploadAsync_ExtensionNotAllowed_Throws()
    {
        var svc = NewService(out var blob, out var policy);
        var file = FakeFile("virus.exe", "application/octet-stream", new byte[10]);

        policy.Setup(p => p.IsExtensionAllowed(MediaType.Photo, ".exe")).Returns(false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.UploadAsync(2, MediaType.Photo, file, "u1"));

        blob.VerifyNoOtherCalls();
        policy.VerifyAll();
    }

    [Fact]
    public async Task UploadAsync_TooLarge_Throws()
    {
        var svc = NewService(out var blob, out var policy);
        var file = FakeFile("big.jpg", "image/jpeg", new byte[200]);

        policy.Setup(p => p.IsExtensionAllowed(MediaType.Photo, ".jpg")).Returns(true);
        policy.Setup(p => p.MaxSizeBytes(MediaType.Photo)).Returns(100); // 100B 上限

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.UploadAsync(3, MediaType.Photo, file, "u1"));

        blob.VerifyNoOtherCalls();
        policy.VerifyAll();
    }

    [Fact]
    public async Task UploadAsync_Success_UploadsToBlob_ReturnsResult()
    {
        var svc = NewService(out var blob, out var policy);
        var file = FakeFile("ok.jpg", "image/jpeg", new byte[1234]);

        policy.Setup(p => p.IsExtensionAllowed(MediaType.Photo, ".jpg")).Returns(true);
        policy.Setup(p => p.MaxSizeBytes(MediaType.Photo)).Returns(10 * 1024 * 1024);
        policy.Setup(p => p.FolderOf(MediaType.Photo)).Returns("photos");

        string? capturedPath = null;
        blob.Setup(b => b.UploadAsync(
                It.Is<Stream>(s => s.Length == 1234),
                It.Is<string>(ct => ct == "image/jpeg"),
                It.Is<string>(p =>
                    p.StartsWith("listings/10/photos/") &&
                    p.EndsWith(".jpg")),
                It.IsAny<CancellationToken>()))
            .Callback<Stream, string, string, CancellationToken>((_, _, path, _) => capturedPath = path)
            .ReturnsAsync(("https://cdn.example/ok.jpg", "image/jpeg", 1234));

        var result = await svc.UploadAsync(10, MediaType.Photo, file, "u1");

        Assert.NotNull(capturedPath);
        Assert.Equal(capturedPath, result.BlobPath);
        Assert.Equal("https://cdn.example/ok.jpg", result.Url);
        Assert.Equal("image/jpeg", result.ContentType);
        Assert.Equal(1234, result.Size);
        Assert.Equal("ok.jpg", result.OriginalFileName);

        policy.VerifyAll();
        blob.VerifyAll();
    }

    [Fact]
    public async Task UploadManyAsync_NotPhoto_Throws()
    {
        var svc = NewService(out var blob, out var policy);
        var files = new List<IFormFile>
        {
            FakeFile("v1.mp4", "video/mp4", new byte[10]),
            FakeFile("v2.mp4", "video/mp4", new byte[10]),
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.UploadManyAsync(1, MediaType.Video, files, "u1"));

        blob.VerifyNoOtherCalls();
        policy.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UploadManyAsync_Photo_Ok_ReturnsList()
    {
        var svc = NewService(out var blob, out var policy);
        var files = new List<IFormFile>
        {
            FakeFile("a.jpg", "image/jpeg", new byte[5]),
            FakeFile("b.jpg", "image/jpeg", new byte[7]),
        };

        policy.Setup(p => p.FolderOf(MediaType.Photo)).Returns("photos");
        policy.Setup(p => p.IsExtensionAllowed(MediaType.Photo, ".jpg")).Returns(true);
        policy.Setup(p => p.MaxSizeBytes(MediaType.Photo)).Returns(10 * 1024 * 1024);

        blob.Setup(b => b.UploadAsync(It.IsAny<Stream>(), "image/jpeg",
                                      It.Is<string>(p => p.Contains("/photos/") && p.EndsWith(".jpg")),
                                      It.IsAny<CancellationToken>()))
            .ReturnsAsync(("https://cdn/x.jpg", "image/jpeg", 5));

        var result = await svc.UploadManyAsync(9, MediaType.Photo, files, "u1");

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.OriginalFileName == "a.jpg");
        Assert.Contains(result, r => r.OriginalFileName == "b.jpg");

        policy.Verify(p => p.IsExtensionAllowed(MediaType.Photo, ".jpg"), Times.Exactly(2));
        blob.Verify(b => b.UploadAsync(It.IsAny<Stream>(), "image/jpeg",
                                       It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}