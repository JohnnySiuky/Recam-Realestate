using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Recam.Models.Entities;
using Recam.Respostitories.Interfaces;
using Recam.Services.Email;

namespace Recam.UnitTests;

public class SqlCaseHistoryServiceTests
{
    private static SqlCaseHistoryService NewSvc(
        out Mock<ICaseHistoryRepository> repo,
        out Mock<ILogger<SqlCaseHistoryService>> logger)
    {
        repo   = new Mock<ICaseHistoryRepository>(MockBehavior.Strict);
        logger = new Mock<ILogger<SqlCaseHistoryService>>(MockBehavior.Loose);
        return new SqlCaseHistoryService(repo.Object, logger.Object);
    }

    // ------- CreatedAsync --------
    [Fact]
    public async Task CreatedAsync_Success_WritesEntity_AndSaves()
    {
        var svc = NewSvc(out var repo, out var logger);

        CaseHistory? captured = null;
        repo.Setup(r => r.AddAsync(It.IsAny<CaseHistory>(), It.IsAny<CancellationToken>()))
            .Callback<CaseHistory, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var payload = new { foo = 1, bar = "x" };
        await svc.CreatedAsync(7, "user-1", payload, default);

        Assert.NotNull(captured);
        Assert.Equal(7, captured!.ListingCaseId);
        Assert.Equal("CREATED", captured.Event);
        Assert.Equal("user-1", captured.ActorUserId);
        Assert.True(captured.AtUtc <= DateTime.Now);
        Assert.Equal(JsonSerializer.Serialize(payload), captured.PayloadJson);

        repo.VerifyAll();
        // 有错才会写 Error，这里不应记录
        logger.Verify(l => l.Log(
            LogLevel.Error, It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task CreatedAsync_NullPayload_WritesNullPayload()
    {
        var svc = NewSvc(out var repo, out var logger);

        CaseHistory? captured = null;
        repo.Setup(r => r.AddAsync(It.IsAny<CaseHistory>(), It.IsAny<CancellationToken>()))
            .Callback<CaseHistory, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await svc.CreatedAsync(9, "op", payload: null, ct: default);

        Assert.NotNull(captured);
        Assert.Equal("CREATED", captured!.Event);
        Assert.Null(captured.PayloadJson);

        repo.VerifyAll();
    }

    // ------- UpdatedAsync --------
    [Fact]
    public async Task UpdatedAsync_Success_WritesEntity_AndSaves()
    {
        var svc = NewSvc(out var repo, out var logger);

        CaseHistory? captured = null;
        repo.Setup(r => r.AddAsync(It.IsAny<CaseHistory>(), It.IsAny<CancellationToken>()))
            .Callback<CaseHistory, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await svc.UpdatedAsync(5, "u-2", new { msg = "ok" }, default);

        Assert.NotNull(captured);
        Assert.Equal("UPDATED", captured!.Event);
        Assert.Equal(JsonSerializer.Serialize(new { msg = "ok" }), captured.PayloadJson);

        repo.VerifyAll();
    }

    // ------- DeletedAsync --------
    [Fact]
    public async Task DeletedAsync_AddThrows_CaughtAndLogged_NoRethrow()
    {
        var svc = NewSvc(out var repo, out var logger);

        repo.Setup(r => r.AddAsync(It.IsAny<CaseHistory>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        // 不应调用 SaveAsync
        await svc.DeletedAsync(3, "actor", new { z = 1 }, default);

        repo.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);

        // 记录了一次 Error
        logger.Verify(l => l.Log(
                LogLevel.Error, It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DeletedAsync_SaveThrows_CaughtAndLogged_NoRethrow()
    {
        var svc = NewSvc(out var repo, out var logger);

        repo.Setup(r => r.AddAsync(It.IsAny<CaseHistory>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("save failed"));

        await svc.DeletedAsync(8, "who", null, default);

        // Add 调用一次，Save 调用一次（但抛出）
        repo.Verify(r => r.AddAsync(It.IsAny<CaseHistory>(), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);

        logger.Verify(l => l.Log(
                LogLevel.Error, It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}