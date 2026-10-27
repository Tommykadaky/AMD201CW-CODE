using Identity.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Data;

// Tao san 1 tai khoan Admin khi service khoi dong (neu chua co).
// Email va mat khau lay tu cau hinh "SeedAdmin" trong appsettings.
public static class AdminSeeder
{
    public static async Task SeedAsync(IdentityDbContext db, IConfiguration config, ILogger logger)
    {
        var email = config["SeedAdmin:Email"]?.Trim().ToLowerInvariant();
        var password = config["SeedAdmin:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Chua cau hinh SeedAdmin, bo qua tao tai khoan Admin.");
            return;
        }

        if (await db.Users.AnyAsync(u => u.Email == email)) return;

        db.Users.Add(new User
        {
            Email = email,
            DisplayName = "Quản trị viên",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = Roles.Admin,
        });
        await db.SaveChangesAsync();
        logger.LogInformation("Da tao tai khoan Admin {Email}", email);
    }
}
