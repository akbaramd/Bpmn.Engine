using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence;

public sealed class BpmnEngineDbContextFactory : IDesignTimeDbContextFactory<BpmnEngineDbContext>
{
    public BpmnEngineDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BpmnEngineDbContext>();

        var conn =
            Environment.GetEnvironmentVariable("ConnectionStrings__BpmnEngine")
            ?? "Host=localhost;Port=5432;Database=bpmn;Username=bpmn;Password=bpmn_pass";

        optionsBuilder.UseNpgsql(conn);
        optionsBuilder.EnableSensitiveDataLogging();

        return new BpmnEngineDbContext(optionsBuilder.Options);
    }
}
