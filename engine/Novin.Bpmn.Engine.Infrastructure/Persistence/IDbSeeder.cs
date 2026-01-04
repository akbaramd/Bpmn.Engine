using Novin.Bpmn.Engine.Infrastructure.Persistence;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Startup;

public interface IDbSeeder
{
    Task SeedAsync(BpmnEngineDbContext db, IServiceProvider sp, CancellationToken ct);
}
