using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Recam.DataAccess;
using Recam.Models.Entities;
using Recam.Models.Enums;

namespace PMS.API.Setup;

public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var db = sp.GetRequiredService<RecamDbContext>();
        var userMgr = sp.GetRequiredService<UserManager<ApplicationUser>>();

        // 拿一個已存在的使用者（先跑過 IdentitySeeder）
        var creator = await userMgr.FindByEmailAsync("photo@recam.test")
                      ?? await userMgr.FindByEmailAsync("admin@recam.test");

        if (creator is null) return; // 沒有使用者就先不 seed 業務資料

        if (!await db.ListingCases.AnyAsync())
        {
            db.ListingCases.Add(new ListingCase
            {
                Title = "Demo Listing",
                Description = "Seeded listing",
                Street = "1 Demo St",
                City = "Sydney",
                State = "NSW",
                PostalCode = 2000,
                Latitude = -33.865143m,
                Longitude = 151.209900m,
                Bedrooms = 3,
                Bathrooms = 2,
                Garages = 1,
                FloorArea = 120.5m,
                PropertyType = PropertyType.House,
                SaleCategory = SaleCategory.ForSale,
                ListingCaseStatus = ListingCaseStatus.Created,
                UserId = creator.Id        // ★ 關鍵：建立者
            });

            await db.SaveChangesAsync();
        }
    }
}