using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Novin.Bpmn.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Novin.Bpmn.Engine.Infrastructure.Common;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core DbContext for BPMN Engine
/// Uses in-memory database for development/testing
/// </summary>
public class BpmnEngineDbContext : DbContext
{
    public DbSet<Deployment> Deployments { get; set; }
    public DbSet<Process> Processes { get; set; }
    public DbSet<Token> Tokens { get; set; }
    public DbSet<Project> Projects { get; set; }

    public DbSet<Incident> Incidents { get; set; }
    public DbSet<BoundaryEventSubscription> BoundaryEventSubscription { get; set; }
    public DbSet<NodeInstance> NodeInstances { get; set; }
    public DbSet<Job?> Jobs { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }
    public DbSet<UserTaskInstance> UserTaskInstances { get; set; }
    
    public BpmnEngineDbContext(DbContextOptions<BpmnEngineDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        // Configure Deployment
    }
}