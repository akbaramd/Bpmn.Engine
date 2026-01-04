using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Infrastructure.Persistence;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Startup;

public static class DatabaseStartupExtensions
{
    public static async Task MigrateAndSeedAsync(this WebApplication app, CancellationToken ct = default)
    {
        using var scope = app.Services.CreateScope();

        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseStartup");

        var db = scope.ServiceProvider.GetRequiredService<BpmnEngineDbContext>();

        // ✅ Retry helps when Postgres container is still coming up
        const int maxRetries = 10;
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                logger.LogInformation("Applying EF migrations (attempt {Attempt}/{Max})...", attempt, maxRetries);
                await db.Database.MigrateAsync(ct);
                break;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                logger.LogWarning(ex, "Migration failed, retrying...");
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }

        var seeders = scope.ServiceProvider.GetServices<IDbSeeder>().ToList();
        if (seeders.Count == 0)
        {
            logger.LogInformation("No seeders registered.");
            return;
        }

        logger.LogInformation("Running {Count} seeders...", seeders.Count);

        foreach (var seeder in seeders)
        {
            await seeder.SeedAsync(db, scope.ServiceProvider, ct);
        }

        logger.LogInformation("Migrations + seeding completed.");
    }
}
