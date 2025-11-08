using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Recam.DataAccess;
using Recam.Models.Entities;

namespace PMS.API.Setup;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        // makesure DB schema is built
        var db = sp.GetRequiredService<RecamDbContext>();
        await db.Database.MigrateAsync();

        var roleMgr = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userMgr = sp.GetRequiredService<UserManager<ApplicationUser>>();

        // role
        string[] roles = { "Admin", "Agent", "PhotographyCompany" };
        foreach (var r in roles)
            if (!await roleMgr.RoleExistsAsync(r))
                await roleMgr.CreateAsync(new IdentityRole(r));

        // test acc
        await CreateUserAsync(userMgr, "admin@recam.test",  "Admin",              "P@ssw0rd!23");
        await CreateUserAsync(userMgr, "agent@recam.test",  "Agent",              "P@ssw0rd!23");
        await CreateUserAsync(userMgr, "photo@recam.test",  "PhotographyCompany", "P@ssw0rd!23");
    }

    private static async Task CreateUserAsync(UserManager<ApplicationUser> um, string email, string role, string pwd)
    {
        var user = await um.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            var res = await um.CreateAsync(user, pwd);
            if (!res.Succeeded)
                throw new InvalidOperationException("Create user failed: " + string.Join("; ", res.Errors.Select(e => e.Description)));
        }
        if (!await um.IsInRoleAsync(user, role))
        {
            var rr = await um.AddToRoleAsync(user, role);
            if (!rr.Succeeded)
                throw new InvalidOperationException("Add role failed: " + string.Join("; ", rr.Errors.Select(e => e.Description)));
        }
    }
}