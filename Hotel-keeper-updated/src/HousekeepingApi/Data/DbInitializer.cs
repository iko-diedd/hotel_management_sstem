using System.Security.Cryptography;
using HousekeepingApi.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HousekeepingApi.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider sp, IConfiguration config, ILogger logger)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var hasher = sp.GetRequiredService<IPasswordHasher<User>>();

        if (config.GetValue("Database:MigrateOnStartup", true))
        {
            if (db.Database.GetMigrations().Any())
                await db.Database.MigrateAsync();
            else
            {
                // No migrations in the project yet: build the schema from the model so a fresh database works.
                // Generate migrations before production use (see README).
                logger.LogWarning("No EF migrations found; creating the schema from the model (EnsureCreated).");
                await db.Database.EnsureCreatedAsync();
            }
        }

        await SeedAdminAsync(db, hasher, config, logger);
        await SeedRoomsAsync(db);
        await SeedDemoStaffAsync(db, hasher, config, logger);
    }

    private static async Task SeedAdminAsync(AppDbContext db, IPasswordHasher<User> hasher, IConfiguration config, ILogger logger)
    {
        if (await db.Users.AnyAsync(u => u.Role == UserRole.Admin)) return;

        var username = config["Seed:AdminUsername"] ?? "admin";
        var password = config["Seed:AdminPassword"];
        var generated = false;
        if (string.IsNullOrWhiteSpace(password))
        {
            password = GeneratePassword();
            generated = true;
        }

        var admin = new User { Name = "System Administrator", Username = username, Role = UserRole.Admin, IsActive = true };
        admin.PasswordHash = hasher.HashPassword(admin, password);
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        if (generated)
            logger.LogWarning("Initial administrator created. Username: {Username}  Password: {Password}  (shown once - change it after first login; or set Seed__AdminPassword before first start)", username, password);
        else
            logger.LogInformation("Initial administrator '{Username}' created from configuration.", username);
    }

    // sample rooms
    private static async Task SeedRoomsAsync(AppDbContext db)
    {
        if (await db.Rooms.AnyAsync()) return;

        var seed = new (string Number, string Type, int Floor, OccupancyStatus Occ, HousekeepingStatus Hk, ServiceStatus Svc)[]
        {
            ("101", "Standard", 1, OccupancyStatus.Vacant, HousekeepingStatus.Clean, ServiceStatus.InService),
            ("102", "Standard", 1, OccupancyStatus.Occupied, HousekeepingStatus.Clean, ServiceStatus.InService),
            ("103", "Deluxe", 1, OccupancyStatus.Vacant, HousekeepingStatus.Dirty, ServiceStatus.InService),
            ("104", "Suite", 1, OccupancyStatus.Vacant, HousekeepingStatus.Clean, ServiceStatus.InService),
            ("105", "Standard", 1, OccupancyStatus.Occupied, HousekeepingStatus.Clean, ServiceStatus.InService),
            ("106", "Standard", 1, OccupancyStatus.Vacant, HousekeepingStatus.Cleaning, ServiceStatus.InService),
            ("107", "Standard", 1, OccupancyStatus.Vacant, HousekeepingStatus.Clean, ServiceStatus.InService),
            ("108", "Deluxe", 1, OccupancyStatus.Occupied, HousekeepingStatus.Clean, ServiceStatus.InService),
            ("109", "Suite", 1, OccupancyStatus.Vacant, HousekeepingStatus.Dirty, ServiceStatus.InService),
            ("110", "Standard", 1, OccupancyStatus.Vacant, HousekeepingStatus.Clean, ServiceStatus.OutOfService),
            ("201", "Standard", 2, OccupancyStatus.Vacant, HousekeepingStatus.Clean, ServiceStatus.InService),
            ("202", "Standard", 2, OccupancyStatus.Occupied, HousekeepingStatus.Clean, ServiceStatus.InService),
            ("203", "Deluxe", 2, OccupancyStatus.Vacant, HousekeepingStatus.Dirty, ServiceStatus.InService),
            ("204", "Suite", 2, OccupancyStatus.Vacant, HousekeepingStatus.Clean, ServiceStatus.InService),
            ("205", "Standard", 2, OccupancyStatus.Occupied, HousekeepingStatus.Clean, ServiceStatus.InService),
            ("206", "Standard", 2, OccupancyStatus.Vacant, HousekeepingStatus.Cleaning, ServiceStatus.InService),
            ("207", "Standard", 2, OccupancyStatus.Vacant, HousekeepingStatus.Clean, ServiceStatus.InService),
            ("208", "Deluxe", 2, OccupancyStatus.Occupied, HousekeepingStatus.Clean, ServiceStatus.InService),
            ("209", "Suite", 2, OccupancyStatus.Vacant, HousekeepingStatus.Dirty, ServiceStatus.InService),
            ("210", "Standard", 2, OccupancyStatus.Vacant, HousekeepingStatus.Clean, ServiceStatus.InService),
        };
        db.Rooms.AddRange(seed.Select(s => new Room
        {
            RoomNumber = s.Number, RoomType = s.Type, Floor = s.Floor,
            OccupancyStatus = s.Occ, HousekeepingStatus = s.Hk, ServiceStatus = s.Svc,
            LastUpdatedAt = DateTime.UtcNow
        }));
        await db.SaveChangesAsync();
    }

    private static async Task SeedDemoStaffAsync(AppDbContext db, IPasswordHasher<User> hasher, IConfiguration config, ILogger logger)
    {
        var password = config["Seed:DemoPassword"];
        if (string.IsNullOrWhiteSpace(password)) return;
        foreach (var (username, name, role) in new[] { ("assistant", "Demo Room Assistant", UserRole.RoomAssistant), ("housekeeper", "Demo Housekeeper", UserRole.Housekeeper) })
        {
            if (await db.Users.AnyAsync(u => u.Username == username)) continue;
            var u = new User { Name = name, Username = username, Role = role, IsActive = true };
            u.PasswordHash = hasher.HashPassword(u, password);
            db.Users.Add(u);
            logger.LogWarning("Demo account '{Username}' created from Seed:DemoPassword. Do not use in production.", username);
        }
        await db.SaveChangesAsync();
    }

    private static string GeneratePassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ", lower = "abcdefghijkmnopqrstuvwxyz", digits = "23456789";
        var all = upper + lower + digits;
        var chars = new List<char> { upper[RandomNumberGenerator.GetInt32(upper.Length)], lower[RandomNumberGenerator.GetInt32(lower.Length)], digits[RandomNumberGenerator.GetInt32(digits.Length)] };
        while (chars.Count < 16) chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);
        return new string(chars.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToArray());
    }
}
