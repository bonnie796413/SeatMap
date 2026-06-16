using BackEnd.Entities;
using Microsoft.AspNetCore.Identity;

namespace BackEnd.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, ILogger logger)
    {
        var db = services.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in new[] { "Admin", "Employee" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        if ((await userManager.GetUsersInRoleAsync("Admin")).Count != 0)
            return;

        var adminPassword = Environment.GetEnvironmentVariable("SEED_ADMIN_PASSWORD") ?? "Admin@12345";
        if (adminPassword == "Admin@12345")
            logger.LogWarning("使用預設管理者密碼，正式環境請設定 SEED_ADMIN_PASSWORD 環境變數");

        var admin = new ApplicationUser
        {
            UserName = "admin",
            Email = "admin@seatmap.local",
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(admin, adminPassword);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(admin, "Admin");
        else
            logger.LogError("建立管理者帳號失敗：{Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
    }
}
