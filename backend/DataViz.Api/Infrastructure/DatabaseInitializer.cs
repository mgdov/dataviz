using DataViz.Api.Data;
using DataViz.Api.Models;
using DataViz.Api.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DataViz.Api.Infrastructure;

/// <summary>
/// Готовит базу данных к работе: ждёт готовности PostgreSQL, накатывает миграции,
/// создаёт администратора по умолчанию и (опционально) запускает сид демо-данных.
/// </summary>
public static class DatabaseInitializer
{
    private const int MaxConnectionAttempts = 30;
    private static readonly TimeSpan AttemptDelay = TimeSpan.FromSeconds(2);

    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await WaitForDatabaseAsync(db, logger);
        await db.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied successfully");

        await EnsureDefaultAdminAsync(db, configuration, logger);

        var seedEnabled = configuration.GetValue<bool>("SeedData")
                          || string.Equals(
                              Environment.GetEnvironmentVariable("SEED_DATA"),
                              "true",
                              StringComparison.OrdinalIgnoreCase);
        if (seedEnabled)
            await DataSeeder.SeedAsync(db, logger);
        else
            logger.LogInformation("Data seeding skipped (SEED_DATA flag not set)");
    }

    private static async Task WaitForDatabaseAsync(ApplicationDbContext db, ILogger logger)
    {
        for (var attempt = 1; attempt <= MaxConnectionAttempts; attempt++)
        {
            try
            {
                await db.Database.OpenConnectionAsync();
                await db.Database.CloseConnectionAsync();
                return;
            }
            catch (NpgsqlException ex) when (attempt < MaxConnectionAttempts)
            {
                logger.LogWarning(
                    "PostgreSQL is not ready yet (attempt {Attempt}/{Max}): {Message}",
                    attempt, MaxConnectionAttempts, ex.Message);
                await Task.Delay(AttemptDelay);
            }
        }

        throw new InvalidOperationException(
            $"Could not connect to PostgreSQL after {MaxConnectionAttempts} attempts");
    }

    private static async Task EnsureDefaultAdminAsync(
        ApplicationDbContext db,
        IConfiguration configuration,
        ILogger logger)
    {
        if (await db.Users.AnyAsync(u => u.Role == UserRoles.Admin))
        {
            logger.LogDebug("Admin user already exists, skipping default admin bootstrap");
            return;
        }

        var email = configuration["Admin:Email"]
                    ?? Environment.GetEnvironmentVariable("ADMIN_EMAIL")
                    ?? "admin@example.com";
        var password = configuration["Admin:Password"]
                       ?? Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
        var name = configuration["Admin:Name"]
                   ?? Environment.GetEnvironmentVariable("ADMIN_NAME")
                   ?? "Admin";

        var generated = false;
        if (string.IsNullOrWhiteSpace(password))
        {
            password = GenerateStrongPassword();
            generated = true;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var existing = await db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
        if (existing is null)
        {
            db.Users.Add(new User
            {
                Name = name,
                Email = normalizedEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = UserRoles.Admin,
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            logger.LogInformation("Created default admin user {Email}", normalizedEmail);
        }
        else
        {
            existing.Role = UserRoles.Admin;
            await db.SaveChangesAsync();
            logger.LogInformation("Promoted existing user {Email} to admin", normalizedEmail);
        }

        if (generated)
        {
            // Логируем сгенерированный пароль один раз — при первом старте, чтобы
            // администратор смог войти и сменить его. В дальнейшем пароль не выводится.
            logger.LogWarning(
                "Generated initial admin password (set ADMIN_PASSWORD to suppress this log): {Password}",
                password);
        }
    }

    private static string GenerateStrongPassword()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#%";
        var bytes = new byte[20];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return string.Create(20, bytes, (span, src) =>
        {
            for (var i = 0; i < span.Length; i++)
                span[i] = alphabet[src[i] % alphabet.Length];
        });
    }
}
