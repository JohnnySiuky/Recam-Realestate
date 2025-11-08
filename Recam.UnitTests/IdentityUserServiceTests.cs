// File: IdentityUserServiceTests.cs
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Moq;
using Xunit;
using Recam.Common.Exceptions;
using Recam.Models.Entities;
using Recam.Services.Email;
using Recam.Services.Logging;
using Recam.Services.Logging.interfaces;

namespace Recam.UnitTests;

public class IdentityUserServiceTests
{
    // ---------- helpers ----------
    private static Mock<UserManager<ApplicationUser>> MockUserManager()
        => new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);

    private static Mock<SignInManager<ApplicationUser>> MockSignInManager(UserManager<ApplicationUser> userMgr)
        => new Mock<SignInManager<ApplicationUser>>(
            userMgr,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            null!, null!, null!, null!);

    private static AuthenticationScheme CookieScheme()
        => new AuthenticationScheme(
            IdentityConstants.ApplicationScheme,          
            "App Cookie",
            typeof(DummyAuthHandler)); 
    
    private sealed class DummyAuthHandler : IAuthenticationHandler
    {
        public Task InitializeAsync(AuthenticationScheme scheme, HttpContext context) 
            => Task.CompletedTask;

        public Task<AuthenticateResult> AuthenticateAsync() 
            => Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(AuthenticationProperties? properties) 
            => Task.CompletedTask;

        public Task ForbidAsync(AuthenticationProperties? properties) 
            => Task.CompletedTask;
    }
    // ========== tests ==========

    [Fact]
    public async Task ChangePassword_UserNotFound_ThrowsNotFound()
    {
        var users   = MockUserManager();
        var schemes = new Mock<IAuthenticationSchemeProvider>();
        var audit   = new Mock<IAuditLogService>();

        users.Setup(x => x.FindByIdAsync("u1")).ReturnsAsync((ApplicationUser?)null);

        var svc = new IdentityUserService(users.Object, null, audit.Object, schemes.Object);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            svc.ChangePasswordAsync("u1", "old", "new#Pass123!"));
    }

    [Fact]
    public async Task ChangePassword_NoExistingPassword_AddsPassword_RefreshesSignIn_Logs()
    {
        var users   = MockUserManager();
        var signIn  = MockSignInManager(users.Object);
        var schemes = new Mock<IAuthenticationSchemeProvider>();
        var audit   = new Mock<IAuditLogService>();

        var user = new ApplicationUser { Id = "u1", Email = "u@example.com", PasswordHash = null };

        users.Setup(x => x.FindByIdAsync("u1")).ReturnsAsync(user);
        users.Setup(x => x.AddPasswordAsync(user, "New#123")).ReturnsAsync(IdentityResult.Success);
        users.Setup(x => x.UpdateSecurityStampAsync(user)).ReturnsAsync(IdentityResult.Success);

        schemes.Setup(x => x.GetAllSchemesAsync())
               .ReturnsAsync(new[] { CookieScheme() });

        signIn.Setup(x => x.RefreshSignInAsync(user)).Returns(Task.CompletedTask);
        audit.Setup(x => x.LogAsync(It.IsAny<AuthAuditLog>(), It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);

        var svc = new IdentityUserService(users.Object, signIn.Object, audit.Object, schemes.Object);

        await svc.ChangePasswordAsync("u1", currentPassword: "", newPassword: "New#123");

        users.Verify(x => x.AddPasswordAsync(user, "New#123"), Times.Once);
        users.Verify(x => x.ChangePasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        users.Verify(x => x.UpdateSecurityStampAsync(user), Times.Once);
        signIn.Verify(x => x.RefreshSignInAsync(user), Times.Once);
        audit.Verify(x => x.LogAsync(It.Is<AuthAuditLog>(l => l.Event == "PASSWORD_CHANGED" && l.UserId == "u1" && l.Email == "u@example.com"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangePassword_NoCookieScheme_DoesNotRefreshSignIn()
    {
        var users   = MockUserManager();
        var signIn  = MockSignInManager(users.Object);
        var schemes = new Mock<IAuthenticationSchemeProvider>();
        var audit   = new Mock<IAuditLogService>();

        var user = new ApplicationUser { Id = "u1", Email = "u@example.com", PasswordHash = null };

        users.Setup(x => x.FindByIdAsync("u1")).ReturnsAsync(user);
        users.Setup(x => x.AddPasswordAsync(user, "New#123")).ReturnsAsync(IdentityResult.Success);
        users.Setup(x => x.UpdateSecurityStampAsync(user)).ReturnsAsync(IdentityResult.Success);

        schemes.Setup(x => x.GetAllSchemesAsync()).ReturnsAsync(new AuthenticationScheme[0]);
        audit.Setup(x => x.LogAsync(It.IsAny<AuthAuditLog>(), It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);

        var svc = new IdentityUserService(users.Object, signIn.Object, audit.Object, schemes.Object);

        await svc.ChangePasswordAsync("u1", "", "New#123");

        signIn.Verify(x => x.RefreshSignInAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task ChangePassword_WithExistingPassword_WrongCurrent_ThrowsValidation()
    {
        var users   = MockUserManager();
        var schemes = new Mock<IAuthenticationSchemeProvider>();
        var audit   = new Mock<IAuditLogService>();

        var user = new ApplicationUser { Id = "u1", Email = "u@example.com", PasswordHash = "hasheddd" };

        users.Setup(x => x.FindByIdAsync("u1")).ReturnsAsync(user);
        users.Setup(x => x.CheckPasswordAsync(user, "wrong")).ReturnsAsync(false);

        var svc = new IdentityUserService(users.Object, null, audit.Object, schemes.Object);

        await Assert.ThrowsAsync<ValidationException>(() =>
            svc.ChangePasswordAsync("u1", "wrong", "New#123"));
        
        users.Verify(x => x.ChangePasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        audit.Verify(x => x.LogAsync(It.IsAny<AuthAuditLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangePassword_WithExistingPassword_CorrectCurrent_ChangesPassword_Refreshes_Logs()
    {
        var users   = MockUserManager();
        var signIn  = MockSignInManager(users.Object);
        var schemes = new Mock<IAuthenticationSchemeProvider>();
        var audit   = new Mock<IAuditLogService>();

        var user = new ApplicationUser { Id = "u1", Email = "u@example.com", PasswordHash = "hasheddd" };

        users.Setup(x => x.FindByIdAsync("u1")).ReturnsAsync(user);
        users.Setup(x => x.CheckPasswordAsync(user, "Old#123")).ReturnsAsync(true);
        users.Setup(x => x.ChangePasswordAsync(user, "Old#123", "New#123")).ReturnsAsync(IdentityResult.Success);
        users.Setup(x => x.UpdateSecurityStampAsync(user)).ReturnsAsync(IdentityResult.Success);

        schemes.Setup(x => x.GetAllSchemesAsync())
               .ReturnsAsync(new[] { CookieScheme() });

        signIn.Setup(x => x.RefreshSignInAsync(user)).Returns(Task.CompletedTask);
        audit.Setup(x => x.LogAsync(It.IsAny<AuthAuditLog>(), It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);

        var svc = new IdentityUserService(users.Object, signIn.Object, audit.Object, schemes.Object);

        await svc.ChangePasswordAsync("u1", "Old#123", "New#123");

        users.Verify(x => x.CheckPasswordAsync(user, "Old#123"), Times.Once);
        users.Verify(x => x.ChangePasswordAsync(user, "Old#123", "New#123"), Times.Once);
        users.Verify(x => x.UpdateSecurityStampAsync(user), Times.Once);
        signIn.Verify(x => x.RefreshSignInAsync(user), Times.Once);
        audit.Verify(x => x.LogAsync(It.Is<AuthAuditLog>(l => l.Event == "PASSWORD_CHANGED" && l.UserId == "u1"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangePassword_AddPasswordFailed_ThrowsValidation()
    {
        var users   = MockUserManager();
        var schemes = new Mock<IAuthenticationSchemeProvider>();
        var audit   = new Mock<IAuditLogService>();

        var user = new ApplicationUser { Id = "u1", Email = "u@example.com", PasswordHash = null };

        users.Setup(x => x.FindByIdAsync("u1")).ReturnsAsync(user);
        users.Setup(x => x.AddPasswordAsync(user, "weak"))
             .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "too weak" }));

        var svc = new IdentityUserService(users.Object, null, audit.Object, schemes.Object);

        await Assert.ThrowsAsync<ValidationException>(() =>
            svc.ChangePasswordAsync("u1", "", "weak"));

        audit.Verify(x => x.LogAsync(It.IsAny<AuthAuditLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangePassword_ChangePasswordFailed_ThrowsValidation()
    {
        var users   = MockUserManager();
        var schemes = new Mock<IAuthenticationSchemeProvider>();
        var audit   = new Mock<IAuditLogService>();

        var user = new ApplicationUser { Id = "u1", Email = "u@example.com", PasswordHash = "hasheddd" };

        users.Setup(x => x.FindByIdAsync("u1")).ReturnsAsync(user);
        users.Setup(x => x.CheckPasswordAsync(user, "Old#123")).ReturnsAsync(true);
        users.Setup(x => x.ChangePasswordAsync(user, "Old#123", "weak"))
             .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "too weak" }));

        var svc = new IdentityUserService(users.Object, null, audit.Object, schemes.Object);

        await Assert.ThrowsAsync<ValidationException>(() =>
            svc.ChangePasswordAsync("u1", "Old#123", "weak"));

        audit.Verify(x => x.LogAsync(It.IsAny<AuthAuditLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}