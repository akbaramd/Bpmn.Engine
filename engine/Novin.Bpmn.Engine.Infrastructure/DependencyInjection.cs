using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Infrastructure.EventBus;
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
        services.AddSingleton(new MultiLanguageScriptTaskExecutorOptions
        {
            TreatNullFormatAsCSharp = true,
            CSharpTimeout = TimeSpan.FromSeconds(2),
            JavaScriptTimeout = TimeSpan.FromSeconds(2),
            JavaScriptMaxStatements = 10_000,
            JavaScriptMaxMemoryBytes = 4_000_000
        });

        // Domain Event Dispatcher
        services.AddScoped<DomainEventDispatcher>();
        // Save Changes Service (handles domain events and causality)
        // Transaction Service (handles transactions directly without UoW abstraction)
        services.AddScoped<ITransactionService, TransactionService>();
        
        // EF Core Repositories (Scoped - one per request/operation, tied to DbContext)
        services.AddScoped<IDeploymentRepository, EfDeploymentRepository>();
        services.AddScoped<IProcessRepository, EfProcessRepository>();
        services.AddScoped<ITokenRepository, EfTokenRepository>();
        services.AddScoped<ITaskRepository, EfTaskRepository>();
        services.AddScoped<IIncidentRepository, EfIncidentRepository>();
        services.AddScoped<IBoundarySubscriptionRepository, EfBoundarySubscriptionRepository>();
        
        // Services
        services.AddScoped<IIncidentService, IncidentService>();
        
        // Unit of Work (Scoped - one per request/operation)
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        
        return services;
    }
}
