// Utils/IdentityTestDoubles.cs
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Recam.Models.Entities;

namespace Recam.UnitTests.Utils
{
    public static class IdentityTestDoubles
    {
        public static Mock<UserManager<ApplicationUser>> CreateUserManager()
        {
            var store       = new Mock<IUserStore<ApplicationUser>>();
            var options     = new Mock<IOptions<IdentityOptions>>();
            options.Setup(o => o.Value).Returns(new IdentityOptions());

            var hasher      = new Mock<IPasswordHasher<ApplicationUser>>();
            var userVals    = new List<IUserValidator<ApplicationUser>>();
            var pwdVals     = new List<IPasswordValidator<ApplicationUser>>();
            var normalizer  = new Mock<ILookupNormalizer>();
            var errors      = new IdentityErrorDescriber();
            var services    = new Mock<IServiceProvider>();
            var logger      = new Mock<ILogger<UserManager<ApplicationUser>>>();

            return new Mock<UserManager<ApplicationUser>>(
                store.Object, options.Object, hasher.Object, userVals, pwdVals,
                normalizer.Object, errors, services.Object, logger.Object);
        }

        public static Mock<RoleManager<IdentityRole>> CreateRoleManager()
        {
            var store      = new Mock<IRoleStore<IdentityRole>>();
            var validators = new List<IRoleValidator<IdentityRole>>();
            var normalizer = new Mock<ILookupNormalizer>();
            var errors     = new IdentityErrorDescriber();
            var logger     = new Mock<ILogger<RoleManager<IdentityRole>>>();

            return new Mock<RoleManager<IdentityRole>>(
                store.Object, validators, normalizer.Object, errors, logger.Object);
        }
    }
}