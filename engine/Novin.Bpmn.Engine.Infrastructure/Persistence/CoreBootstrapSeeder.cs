using Microsoft.EntityFrameworkCore;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Infrastructure.Persistence;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Startup;

public sealed class CoreBootstrapSeeder : IDbSeeder
{
    public async Task SeedAsync(BpmnEngineDbContext db, IServiceProvider sp, CancellationToken ct)
    {
        // Example only — adjust to your actual entities.
        // Goal: seed minimal "system" records once.

        // ✅ Idempotent check
        var hasAny = await db.Set<Project>().AnyAsync(ct);
        if (hasAny) return;

        var project = Project.Create(Guid.Parse("6f7c9c6a-7b8b-4b84-8a7a-1c2a3b4c5d6e"),"test","test","test");
        
        db.Add(project);

        await db.SaveChangesAsync(ct);
    }
}
