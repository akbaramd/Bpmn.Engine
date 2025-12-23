using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Infrastructure.EventBus;
using Novin.Bpmn.Engine.Infrastructure.EventStore;
using Novin.Bpmn.Engine.Infrastructure.Persistence;
using Novin.Bpmn.Engine.Infrastructure.Persistence.Repositories;
using Novin.Bpmn.Engine.Infrastructure.Persistence.UnitOfWork;

namespace Novin.Bpmn.Engine.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Entity Framework Core with In-Memory Database
        services.AddDbContext<BpmnEngineDbContext>(options =>
        {
            options.UseSqlite("Filename=./Bpmn.db");
            options.EnableSensitiveDataLogging(); // For development only
        });

        // Event Bus
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        
        // Event Store
        services.AddSingleton<IEventStore, InMemoryEventStore>();
        
        // Domain Event Dispatcher
        services.AddScoped<DomainEventDispatcher>();
        
        // EF Core Repositories (Scoped - one per request/operation, tied to DbContext)
        services.AddScoped<IDeploymentRepository, EfDeploymentRepository>();
        services.AddScoped<IProcessRepository, EfProcessRepository>();
        services.AddScoped<INodeRepository, EfNodeRepository>();
        services.AddScoped<ITokenRepository, EfTokenRepository>();
        services.AddScoped<ITaskRepository, EfTaskRepository>();
        
        // Unit of Work (Scoped - one per request/operation)
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        
        return services;
    }
}
