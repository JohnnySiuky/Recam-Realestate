using Moq;
using Recam.Services.Email;
using Recam.Services.Interfaces;
using Recam.Respostitories.Interfaces;
using Recam.Common.Exceptions;
using Recam.Models.Entities;

namespace Recam.UnitTests;

public class MediaServiceTests
{
    private static MediaService NewService(
        out Mock<IMediaRepository> mediaRepo,
        out Mock<IListingCaseRepository> listingRepo,
        out Mock<ICaseHistoryService> history,
        out Mock<IBlobStorageService> blob)
    {
        mediaRepo   = new Mock<IMediaRepository>(MockBehavior.Strict);
        listingRepo = new Mock<IListingCaseRepository>(MockBehavior.Loose);
        history     = new Mock<ICaseHistoryService>(MockBehavior.Strict);
        blob        = new Mock<IBlobStorageService>(MockBehavior.Strict);

        return new MediaService(mediaRepo.Object, listingRepo.Object, history.Object, blob.Object);
    }

    [Fact]
    public async Task DeleteAsync_NotFound_Throws()
    {
        var svc = NewService(out var mediaRepo, out _, out var history, out var blob);

        mediaRepo.Setup(r => r.GetByIdAsync(123, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((MediaAsset?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            svc.DeleteAsync(123, "op", CancellationToken.None));

        mediaRepo.VerifyAll();
        blob.VerifyNoOtherCalls();
        history.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DeleteAsync_AlreadyDeleted_ReturnsFalse_NoSideEffects()
    {
        var svc = NewService(out var mediaRepo, out _, out var history, out var blob);

        var asset = new MediaAsset { Id = 10, ListingCaseId = 7, MediaUrl = "blob://m1.jpg", IsDeleted = true };

        mediaRepo.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(asset);

        var ok = await svc.DeleteAsync(10, "user-1", CancellationToken.None);

        Assert.False(ok);
        mediaRepo.VerifyAll();
        blob.Verify(b => b.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        mediaRepo.Verify(r => r.SoftDeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        history.Verify(h => h.DeletedAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeleteTrue_CallsBlobAndHistory_ReturnsTrue()
    {
        var svc = NewService(out var mediaRepo, out _, out var history, out var blob);

        var asset = new MediaAsset { Id = 11, ListingCaseId = 99, MediaUrl = "blob://m2.jpg", IsDeleted = false };

        mediaRepo.Setup(r => r.GetByIdAsync(11, It.IsAny<CancellationToken>())).ReturnsAsync(asset);
        blob.Setup(b => b.DeleteAsync("blob://m2.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // ✅ Task<bool>
        mediaRepo.Setup(r => r.SoftDeleteAsync(11, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);
        history.Setup(h => h.DeletedAsync(99, "admin-1", It.Is<object>(o => o != null), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);

        var ok = await svc.DeleteAsync(11, "admin-1", CancellationToken.None);

        Assert.True(ok);
        mediaRepo.VerifyAll();
        blob.VerifyAll();
        history.VerifyAll();
    }

    [Fact]
    public async Task DeleteAsync_SoftDeleteFalse_CallsBlob_NoHistory_ReturnsFalse()
    {
        var svc = NewService(out var mediaRepo, out _, out var history, out var blob);

        var asset = new MediaAsset { Id = 12, ListingCaseId = 5, MediaUrl = "blob://m3.jpg", IsDeleted = false };

        mediaRepo.Setup(r => r.GetByIdAsync(12, It.IsAny<CancellationToken>())).ReturnsAsync(asset);
        blob.Setup(b => b.DeleteAsync("blob://m3.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // ✅ Task<bool>
        mediaRepo.Setup(r => r.SoftDeleteAsync(12, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(false);

        var ok = await svc.DeleteAsync(12, "user-x", CancellationToken.None);

        Assert.False(ok);
        mediaRepo.VerifyAll();
        blob.VerifyAll();
        history.Verify(h => h.DeletedAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_BlobThrows_StillSoftDeletes_AndLogs()
    {
        var svc = NewService(out var mediaRepo, out _, out var history, out var blob);

        var asset = new MediaAsset { Id = 13, ListingCaseId = 77, MediaUrl = "blob://m4.jpg", IsDeleted = false };

        mediaRepo.Setup(r => r.GetByIdAsync(13, It.IsAny<CancellationToken>())).ReturnsAsync(asset);
        blob.Setup(b => b.DeleteAsync("blob://m4.jpg", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("blob err"));
        mediaRepo.Setup(r => r.SoftDeleteAsync(13, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);
        history.Setup(h => h.DeletedAsync(77, "op-1", It.Is<object>(o => o != null), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);

        var ok = await svc.DeleteAsync(13, "op-1", CancellationToken.None);

        Assert.True(ok);
        mediaRepo.VerifyAll();
        blob.VerifyAll();      // 仍会调用（异常被吞）
        history.VerifyAll();
    }
}