using FluentAssertions;
using Moq;
using Recam.Common.Exceptions;
using Recam.Common.Extensions;
using Recam.Models.Entities;
using Recam.Models.Enums;
using Recam.Respostitories.Interfaces;
using Recam.Services.DTOs;
using Recam.Services.Email;
using Recam.Services.Interfaces;
using Recam.Services.Logging;

namespace Recam.UnitTests;

public class ListingCaseServiceTests
{
    private static CreateListingCaseRequest NewCreateReq() => new CreateListingCaseRequest(
        Title: "Nice House",
        Description: "desc",
        Street: "1 Main St",
        City: "Sydney",
        State: "NSW",
        PostalCode: 2000,
        PropertyType: PropertyType.House,
        SaleCategory: SaleCategory.ForSale,
        Bedrooms: 3,
        Bathrooms: 2,
        Garages: 1,
        FloorArea: 120m,
        Price: 1_000_000m,
        Latitude: -33.86m,
        Longitude: 151.21m
    );


    private static UpdateListingCaseRequest NewUpdateReq() => new UpdateListingCaseRequest(
        Title: "Updated",
        Description: "NewDesc",
        Street: "12 Rose",
        City: "Chatswood",
        State: "NSW",
        PostalCode: 2067,
        PropertyType: PropertyType.House,
        SaleCategory: SaleCategory.ForSale,
        Bedrooms: 4,
        Bathrooms: 3,
        Garages: 2,
        FloorArea: 220m,
        Price: 1_950_000m,
        Latitude: -33.79m,
        Longitude: 151.18m
    );

    private static ListingCaseService BuildService(
        out Mock<IListingCaseRepository> repo,
        out Mock<ICaseHistoryService> history,
        out Mock<IMediaRepository> mediaRepo,
        out Mock<IMediaAssetRepository> mediaAssetRepo,
        out Mock<IListingAuditLogService> audit,
        string baseUrl = "https://recam.app/p")
    {
        repo = new Mock<IListingCaseRepository>();
        history = new Mock<ICaseHistoryService>();
        mediaRepo = new Mock<IMediaRepository>();
        mediaAssetRepo = new Mock<IMediaAssetRepository>();
        audit = new Mock<IListingAuditLogService>();

        var opt = new PublicListingOptions { BaseUrl = baseUrl };
        return new ListingCaseService(repo.Object, history.Object, mediaRepo.Object, mediaAssetRepo.Object, opt, audit.Object);
    }

    // ---------- Create ----------
    [Fact]
    public async Task Create_Should_Succeed_When_Authorized()
    {
        var svc = BuildService(out var repo, out var history, out _, out _, out _);

        repo.Setup(r => r.AddAsync(It.IsAny<ListingCase>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        history.Setup(h => h.CreatedAsync(
                It.IsAny<int>(), "u1", It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var dto = await svc.CreateAsync("u1", canCreate: true, NewCreateReq(), default);

        dto.Title.Should().Be("Nice House"); // 已 Trim
        repo.Verify(r => r.AddAsync(It.IsAny<ListingCase>(), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
        history.Verify(h => h.CreatedAsync(It.IsAny<int>(), "u1", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_Should_Throw_Forbidden_When_Not_Allowed()
    {
        var svc = BuildService(out var repo, out var history, out _, out _, out _);

        var act = () => svc.CreateAsync("u1", canCreate: false, NewCreateReq(), default);

        await act.Should().ThrowAsync<ForbiddenException>();
        repo.Verify(r => r.AddAsync(It.IsAny<ListingCase>(), It.IsAny<CancellationToken>()), Times.Never);
        history.VerifyNoOtherCalls();
    }

    // ---------- Update ----------
    [Fact]
    public async Task Update_Should_Throw_Forbidden_When_Not_Admin()
    {
        var svc = BuildService(out _, out _, out _, out _, out _);
        var act = () => svc.UpdateAsync(3, "u1", isAdmin: false, NewUpdateReq(), default);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Update_Should_Throw_NotFound_When_Missing()
    {
        var svc = BuildService(out var repo, out _, out _, out _, out _);
        repo.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync((ListingCase?)null);

        var act = () => svc.UpdateAsync(3, "admin", isAdmin: true, NewUpdateReq(), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Update_Should_Throw_Conflict_When_Deleted()
    {
        var svc = BuildService(out var repo, out _, out _, out _, out _);
        repo.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListingCase { Id = 3, IsDeleted = true });

        var act = () => svc.UpdateAsync(3, "admin", isAdmin: true, NewUpdateReq(), default);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Update_Should_Save_Changes_And_Write_History()
    {
        var svc = BuildService(out var repo, out var history, out _, out _, out _);
        var entity = new ListingCase
        {
            Id = 3, Title = "Old", City = "OldCity", State = "NSW",
            PostalCode = 2000, PropertyType = PropertyType.House, SaleCategory = SaleCategory.ForSale
        };

        repo.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        repo.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var dto = await svc.UpdateAsync(3, "admin", isAdmin: true, NewUpdateReq(), default);

        dto.Title.Should().Be("Updated");
        repo.Verify(r => r.Update(entity), Times.Once);
        repo.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
        history.Verify(h => h.UpdatedAsync(3, "admin", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- Delete ----------
    [Fact]
    public async Task Delete_Should_Throw_Forbidden_When_Not_Admin()
    {
        var svc = BuildService(out _, out _, out _, out _, out _);
        var act = () => svc.DeleteAsync(9, "u1", isAdmin: false, default);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Delete_Should_Throw_NotFound_When_SoftDelete_Returns_False()
    {
        var svc = BuildService(out var repo, out _, out _, out _, out _);
        repo.Setup(r => r.SoftDeleteCascadeAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var act = () => svc.DeleteAsync(9, "admin", isAdmin: true, default);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Delete_Should_Write_History_When_Success()
    {
        var svc = BuildService(out var repo, out var history, out _, out _, out _);
        repo.Setup(r => r.SoftDeleteCascadeAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await svc.DeleteAsync(9, "admin", isAdmin: true, default);

        history.Verify(h => h.DeletedAsync(9, "admin", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- Change Status ----------
    [Fact]
    public async Task ChangeStatus_Should_Throw_NotFound_When_Entity_Missing()
    {
        var svc = BuildService(out var repo, out _, out _, out _, out _);
        repo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync((ListingCase?)null);

        var act = () => svc.ChangeStatusAsync(5, "admin", new[] { "Admin" }, ListingCaseStatus.Pending, null, default);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ChangeStatus_Should_Throw_Forbidden_When_Not_Admin_Or_OwnerCompany()
    {
        var svc = BuildService(out var repo, out _, out _, out _, out _);
        repo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListingCase { Id = 5, UserId = "owner", ListingCaseStatus = ListingCaseStatus.Created });

        // 角色：PhotographyCompany 但不是 owner
        var act = () => svc.ChangeStatusAsync(5, "otherCompany", new[] { "PhotographyCompany" }, ListingCaseStatus.Pending, null, default);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task ChangeStatus_Should_Throw_Conflict_When_Same_Status()
    {
        var svc = BuildService(out var repo, out _, out _, out _, out _);
        repo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListingCase { Id = 5, UserId = "owner", ListingCaseStatus = ListingCaseStatus.Created });

        var act = () => svc.ChangeStatusAsync(5, "owner", new[] { "Admin" }, ListingCaseStatus.Created, null, default);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task ChangeStatus_Should_Throw_Validation_When_Illegal_Transition()
    {
        var svc = BuildService(out var repo, out _, out _, out _, out _);
        // Created -> Delivered 不允许
        repo.Setup(r => r.GetByIdAsync(8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListingCase { Id = 8, UserId = "owner", ListingCaseStatus = ListingCaseStatus.Created });

        var act = () => svc.ChangeStatusAsync(8, "owner", new[] { "Admin" }, ListingCaseStatus.Delivered, null, default);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task ChangeStatus_Should_Update_And_Write_History_On_Success()
    {
        var svc = BuildService(out var repo, out var history, out _, out _, out _);
        var entity = new ListingCase { Id = 10, UserId = "owner", ListingCaseStatus = ListingCaseStatus.Created };
        repo.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        repo.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var resp = await svc.ChangeStatusAsync(10, "admin", new[] { "Admin" }, ListingCaseStatus.Pending, "go", default);

        resp.OldStatus.Should().Be(ListingCaseStatus.Created);
        resp.NewStatus.Should().Be(ListingCaseStatus.Pending);
        repo.Verify(r => r.Update(entity), Times.Once);
        repo.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
        history.Verify(h => h.UpdatedAsync(10, "admin", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- Publish ----------
    [Fact]
    public async Task Publish_Should_Throw_Forbidden_When_Role_Not_Allowed()
    {
        var svc = BuildService(out _, out _, out _, out _, out _);
        var act = () => svc.PublishAsync(3, "u1", new[] { "Agent" }, default);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Publish_Should_Throw_NotFound_When_Entity_Missing()
    {
        var svc = BuildService(out var repo, out _, out _, out _, out _);
        repo.Setup(r => r.GetForUpdateAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync((ListingCase?)null);

        var act = () => svc.PublishAsync(3, "admin", new[] { "Admin" }, default);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Publish_Should_Throw_Forbidden_When_Company_Not_Owner()
    {
        var svc = BuildService(out var repo, out _, out _, out _, out _);
        repo.Setup(r => r.GetForUpdateAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListingCase { Id = 3, UserId = "owner", ListingCaseStatus = ListingCaseStatus.Pending });

        var act = () => svc.PublishAsync(3, "otherCompany", new[] { "PhotographyCompany" }, default);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Publish_Should_Throw_BadRequest_When_Deleted()
    {
        var svc = BuildService(out var repo, out _, out _, out _, out _);
        repo.Setup(r => r.GetForUpdateAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListingCase { Id = 3, UserId = "owner", IsDeleted = true, ListingCaseStatus = ListingCaseStatus.Pending });

        var act = () => svc.PublishAsync(3, "owner", new[] { "PhotographyCompany" }, default);
        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task Publish_FirstTime_Should_Set_PublicUrl_Save_And_Write_Audit()
    {
        var svc = BuildService(out var repo, out _, out _, out _, out var audit, baseUrl: "https://recam.app/p");
        var entity = new ListingCase { Id = 7, UserId = "owner", ListingCaseStatus = ListingCaseStatus.Pending, PublicUrl = null };
        repo.Setup(r => r.GetForUpdateAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        repo.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        audit.Setup(a => a.LogAsync(
                It.Is<ListingPublishLog>(l =>
                    l.ListingCaseId == 7 &&
                    l.OperatorUserId == "owner" &&
                    l.Event == "LISTING_PUBLISHED" &&
                    l.PublicUrl!.StartsWith("https://recam.app/p/")),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var resp = await svc.PublishAsync(7, "owner", new[] { "PhotographyCompany" }, default);

        resp.PublicUrl.Should().StartWith("https://recam.app/p/");
        repo.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
        audit.VerifyAll();
    }

    [Fact]
    public async Task Publish_Again_Should_Not_Save_But_Write_Again_Audit()
    {
        var svc = BuildService(out var repo, out _, out _, out _, out var audit, baseUrl: "https://recam.app/p");
        var entity = new ListingCase { Id = 7, UserId = "owner", ListingCaseStatus = ListingCaseStatus.Pending, PublicUrl = "https://recam.app/p/abc" };
        repo.Setup(r => r.GetForUpdateAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        audit.Setup(a => a.LogAsync(
                It.Is<ListingPublishLog>(l =>
                    l.ListingCaseId == 7 &&
                    l.OperatorUserId == "owner" &&
                    l.Event == "LISTING_PUBLISHED_AGAIN" &&
                    l.PublicUrl == "https://recam.app/p/abc"),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var resp = await svc.PublishAsync(7, "owner", new[] { "PhotographyCompany" }, default);

        resp.PublicUrl.Should().Be("https://recam.app/p/abc");
        repo.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Never); // 再次发布不写库
        audit.VerifyAll();
    }
}