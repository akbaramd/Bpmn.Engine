using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence;

public class BpmnEngineDbContextFactory : IDesignTimeDbContextFactory<BpmnEngineDbContext>
{
    public BpmnEngineDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BpmnEngineDbContext>();
        optionsBuilder.UseSqlite("Filename=./Bpmn.db");
        optionsBuilder.EnableSensitiveDataLogging();

        return new BpmnEngineDbContext(optionsBuilder.Options);
    }
}
