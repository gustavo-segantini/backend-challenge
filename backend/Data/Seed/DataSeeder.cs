using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CnabApi.Models;

namespace CnabApi.Data.Seed;

public static class DataSeeder
{
    private const string DefaultAdminUsername = "admin";
    private const string DefaultAdminPassword = "Admin123!"; // para dev/test; substituir em prod via configuração
    private const string DefaultAdminEmail = "admin@example.com";

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CnabDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var logger = scope.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger("DataSeeder");

        if (db.Database.IsRelational())
        {
            await db.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            // InMemory provider used in tests does not support migrations
            await db.Database.EnsureCreatedAsync(cancellationToken);
        }

        var admin = await db.Users.FirstOrDefaultAsync(u => u.Username == DefaultAdminUsername, cancellationToken);
        if (admin is null)
        {
            admin = new User
            {
                Username = DefaultAdminUsername,
                Email = DefaultAdminEmail,
                Role = "Admin"
            };
            admin.PasswordHash = hasher.HashPassword(admin, DefaultAdminPassword);
            db.Users.Add(admin);
            await db.SaveChangesAsync(cancellationToken);
            logger?.LogInformation("Seeded default admin user (username: {Username})", DefaultAdminUsername);
        }
    }
}
