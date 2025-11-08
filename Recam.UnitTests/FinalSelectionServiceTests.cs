using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Recam.Common.Exceptions;
using Recam.Models.Entities;
using Recam.Models.Enums;
using Recam.Respostitories.Interfaces;
using Recam.Services.Email;
using Recam.Services.Logging.interfaces;
using Recam.Services.DTOs;
using Recam.UnitTests.Testing; // ToAsyncQueryable()

namespace Recam.UnitTests;

public class FinalSelectionServiceTests
{
    private static IReadOnlyCollection<string> Roles(params string[] rs) => rs;

    private static FinalSelectionService NewService(
        out Mock<IListingCaseRepository> listingRepo,
        out Mock<ISelectedMediaRepository> selectedRepo,
        out Mock<IMediaAssetRepository> mediaRepo,
        out Mock<IMediaSelectionLogService> logSvc,
        out Mock<ILogger<FinalSelectionService>> logger)
    {
        listingRepo = new Mock<IListingCaseRepository>();
        selectedRepo = new Mock<ISelectedMediaRepository>();
        mediaRepo = new Mock<IMediaAssetRepository>();
        logSvc = new Mock<IMediaSelectionLogService>();
        logger = new Mock<ILogger<FinalSelectionService>>();

        return new FinalSelectionService(
            listingRepo.Object,
            selectedRepo.Object,
            mediaRepo.Object,
            logSvc.Object,
            logger.Object
        );
    }

    // ==================== GetAsync ====================

    [Fact]
    public async Task GetAsync_AdminAndDelivered_ReturnsFinalItems()
    {
        var svc = NewService(out var listingRepo, out var selectedRepo, out var mediaRepo, out var logSvc, out var logger);

        // listing Delivered
        listingRepo.Setup(r => r.Query()).Returns(new[]
        {
            new ListingCase { Id = 123, IsDeleted = false, ListingCaseStatus = ListingCaseStatus.Delivered }
        }.ToAsyncQueryable());

        // 2 条最终选择，包含 MediaAsset 导航
        selectedRepo.Setup(r => r.Query()).Returns(new[]
        {
            new SelectedMedia
            {
                ListingCaseId = 123, MediaAssetId = 10, IsFinal = true, SelectedAt = DateTime.UtcNow.AddMinutes(-2),
                AgentId = "agent-1",
                MediaAsset = new MediaAsset { Id = 10, ListingCaseId = 123, MediaType = MediaType.Photo, MediaUrl = "url/p1.jpg", IsHero = true, IsDeleted = false }
            },
            new SelectedMedia
            {
                ListingCaseId = 123, MediaAssetId = 11, IsFinal = true, SelectedAt = DateTime.UtcNow.AddMinutes(-1),
                AgentId = "agent-1",
                MediaAsset = new MediaAsset { Id = 11, ListingCaseId = 123, MediaType = MediaType.Video, MediaUrl = "url/v1.mp4", IsHero = false, IsDeleted = false }
            }
        }.ToAsyncQueryable());

        var resp = await svc.GetAsync(123, "admin-1", Roles("Admin"));

        Assert.Equal(123, resp.ListingCaseId);
        Assert.Equal(ListingCaseStatus.Delivered, resp.Status);
        Assert.Equal(2, resp.Count);
        Assert.Collection(resp.Items,
            i => { Assert.Equal(10, i.MediaAssetId); Assert.True(i.IsHero); Assert.Equal(MediaType.Photo, i.MediaType); },
            i => { Assert.Equal(11, i.MediaAssetId); Assert.False(i.IsHero); Assert.Equal(MediaType.Video, i.MediaType); }
        );
    }

    [Fact]
    public async Task GetAsync_AgentNotAssigned_ThrowsForbidden()
    {
        var svc = NewService(out var listingRepo, out var selectedRepo, out var mediaRepo, out var logSvc, out var logger);

        // listing Delivered
        listingRepo.Setup(r => r.Query()).Returns(new[]
        {
            new ListingCase { Id = 123, IsDeleted = false, ListingCaseStatus = ListingCaseStatus.Delivered }
        }.ToAsyncQueryable());

        // 没有分配到该 agent
        listingRepo.Setup(r => r.AgentAssignments()).Returns(Array.Empty<AgentListingCase>().ToAsyncQueryable());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            svc.GetAsync(123, "agent-1", Roles("Agent")));
    }

    [Fact]
    public async Task GetAsync_NotDelivered_ThrowsBadRequest()
    {
        var svc = NewService(out var listingRepo, out _, out _, out _, out _);

        listingRepo.Setup(r => r.Query()).Returns(new[]
        {
            new ListingCase { Id = 123, IsDeleted = false, ListingCaseStatus = ListingCaseStatus.Pending }
        }.ToAsyncQueryable());

        await Assert.ThrowsAsync<BadRequestException>(() =>
            svc.GetAsync(123, "admin-1", Roles("Admin")));
    }

    [Fact]
    public async Task GetAsync_NotFound_ThrowsNotFound()
    {
        var svc = NewService(out var listingRepo, out _, out _, out _, out _);

        listingRepo.Setup(r => r.Query()).Returns(Array.Empty<ListingCase>().ToAsyncQueryable());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            svc.GetAsync(123, "admin-1", Roles("Admin")));
    }

    // ==================== SaveAgentSelectionAsync ====================

    [Fact]
    public async Task SaveSelection_Admin_Success_ClearsOld_InsertsNew_Logs()
    {
        var svc = NewService(out var listingRepo, out var selectedRepo, out var mediaRepo, out var logSvc, out var logger);

        // listing Delivered
        listingRepo.Setup(r => r.Query()).Returns(new[]
        {
            new ListingCase { Id = 123, IsDeleted = false, UserId = "owner", ListingCaseStatus = ListingCaseStatus.Delivered }
        }.ToAsyncQueryable());

        // Admin无须分配，给空
        listingRepo.Setup(r => r.AgentAssignments()).Returns(Array.Empty<AgentListingCase>().ToAsyncQueryable());

        var ids = new[] { 100, 101, 102 };
        mediaRepo.Setup(r => r.GetByIdsAsync(
                It.Is<IReadOnlyCollection<int>>(x => x.SequenceEqual(ids)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MediaAsset>
            {
                new() { Id = 100, ListingCaseId = 123, IsDeleted = false, MediaType = MediaType.Photo },
                new() { Id = 101, ListingCaseId = 123, IsDeleted = false, MediaType = MediaType.Photo },
                new() { Id = 102, ListingCaseId = 123, IsDeleted = false, MediaType = MediaType.Video },
            });

        selectedRepo.Setup(r => r.DeleteByListingAsync(123, It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

        List<SelectedMedia>? inserted = null;
        selectedRepo.Setup(r => r.AddRangeAsync(
                It.IsAny<IEnumerable<SelectedMedia>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<SelectedMedia>, CancellationToken>((rows, _) => inserted = rows.ToList())
            .Returns(Task.CompletedTask);

        selectedRepo.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

        logSvc.Setup(l => l.LogSelectionAsync(
                123,
                "admin-1",
                It.Is<IReadOnlyCollection<int>>(x => x.SequenceEqual(ids)),
                It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);

        await svc.SaveAgentSelectionAsync(123, "admin-1", Roles("Admin"), ids, markFinal: true);

        selectedRepo.Verify(r => r.DeleteByListingAsync(123, It.IsAny<CancellationToken>()), Times.Once);
        selectedRepo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<SelectedMedia>>(), It.IsAny<CancellationToken>()), Times.Once);
        selectedRepo.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
        logSvc.VerifyAll();

        Assert.NotNull(inserted);
        Assert.Equal(3, inserted!.Count);
        Assert.All(inserted!, row =>
        {
            Assert.Equal(123, row.ListingCaseId);
            Assert.Contains(row.MediaAssetId, ids);
            Assert.True(row.IsFinal);           // Admin + markFinal=true
            Assert.Null(row.AgentId);           // Admin 提交 => AgentId=null
        });
    }

    [Fact]
    public async Task SaveSelection_AgentNotAssigned_ThrowsForbidden()
    {
        var svc = NewService(out var listingRepo, out _, out var mediaRepo, out _, out _);

        listingRepo.Setup(r => r.Query()).Returns(new[]
        {
            new ListingCase { Id = 123, IsDeleted = false, ListingCaseStatus = ListingCaseStatus.Delivered }
        }.ToAsyncQueryable());

        // 分配给了别人
        listingRepo.Setup(r => r.AgentAssignments()).Returns(new[]
        {
            new AgentListingCase { ListingCaseId = 123, AgentId = "other-agent" }
        }.ToAsyncQueryable());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            svc.SaveAgentSelectionAsync(123, "agent-1", Roles("Agent"), new[] { 1 }, false));
    }

    [Fact]
    public async Task SaveSelection_EmptyIds_ThrowsBadRequest()
    {
        var svc = NewService(out _, out _, out _, out _, out _);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            svc.SaveAgentSelectionAsync(123, "admin-1", Roles("Admin"), Array.Empty<int>(), false));
    }

    [Fact]
    public async Task SaveSelection_InvalidIdsCountMismatch_ThrowsBadRequest()
    {
        var svc = NewService(out var listingRepo, out _, out var mediaRepo, out _, out _);

        listingRepo.Setup(r => r.Query()).Returns(new[]
        {
            new ListingCase { Id = 123, IsDeleted = false, ListingCaseStatus = ListingCaseStatus.Delivered }
        }.ToAsyncQueryable());

        listingRepo.Setup(r => r.AgentAssignments()).Returns(Array.Empty<AgentListingCase>().ToAsyncQueryable());

        var ids = new[] { 1, 2 };
        mediaRepo.Setup(r => r.GetByIdsAsync(
                It.IsAny<IReadOnlyCollection<int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MediaAsset> {
                new() { Id = 1, ListingCaseId = 123, IsDeleted = false }
            });

        await Assert.ThrowsAsync<BadRequestException>(() =>
            svc.SaveAgentSelectionAsync(123, "admin-1", Roles("Admin"), ids, false));
    }

    [Fact]
    public async Task SaveSelection_MediaNotBelongToListing_ThrowsBadRequest()
    {
        var svc = NewService(out var listingRepo, out _, out var mediaRepo, out _, out _);

        listingRepo.Setup(r => r.Query()).Returns(new[]
        {
            new ListingCase { Id = 123, IsDeleted = false, ListingCaseStatus = ListingCaseStatus.Delivered }
        }.ToAsyncQueryable());

        listingRepo.Setup(r => r.AgentAssignments()).Returns(Array.Empty<AgentListingCase>().ToAsyncQueryable());

        var ids = new[] { 100 };
        mediaRepo.Setup(r => r.GetByIdsAsync(
                It.IsAny<IReadOnlyCollection<int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MediaAsset> {
                new() { Id = 100, ListingCaseId = 999, IsDeleted = false }
            });

        await Assert.ThrowsAsync<BadRequestException>(() =>
            svc.SaveAgentSelectionAsync(123, "admin-1", Roles("Admin"), ids, false));
    }
}