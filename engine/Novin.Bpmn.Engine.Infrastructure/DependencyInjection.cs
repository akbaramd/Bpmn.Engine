using Elastic.Clients.Elasticsearch;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Application.Services.Interfaces.Infrastructure;
using Novin.Bpmn.Engine.Domain.DomainServices;
using Novin.Bpmn.Engine.Domain.Repositories;
using Novin.Bpmn.Engine.Infrastructure.Common;
using Novin.Bpmn.Engine.Infrastructure.EventBus;
using Novin.Bpmn.Engine.Infrastructure.Outbox.Redis;
using Novin.Bpmn.Engine.Infrastructure.Persistence;
using Novin.Bpmn.Engine.Infrastructure.Persistence.Interceptors;
using Novin.Bpmn.Engine.Infrastructure.Persistence.Outbox;
using Novin.Bpmn.Engine.Infrastructure.Persistence.Repositories;
using Novin.Bpmn.Engine.Infrastructure.Persistence.Startup;
using Novin.Bpmn.Engine.Infrastructure.Persistence.UnitOfWork;
using IDeploymentRepository = Novin.Bpmn.Engine.Application.Common.Interfaces.IDeploymentRepository;

namespace Novin.Bpmn.Engine.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {

        IConfiguration configuration = services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IConfiguration>();
        // Entity Framework Core with In-Memory Database
        services.AddDbContext<BpmnEngineDbContext>(options =>
            {
                // Prefer config, but hard-coded shown for clarity.
                // If API is running in Docker, Host should be "postgres" (service name).
                // If API is running locally, Host should be "localhost".
                var conn =
                    "Host=localhost;Port=5432;Database=bpmn;Username=bpmn;Password=bpmn_pass;" +
                    "Pooling=true;Maximum Pool Size=200;Timeout=15;Command Timeout=30";

                options.UseNpgsql(conn);

                options.EnableSensitiveDataLogging(); // dev only
            });
        services.AddSingleton(new MultiLanguageScriptTaskExecutorOptions
        {
            TreatNullFormatAsCSharp = true,
            CSharpTimeout = TimeSpan.FromSeconds(30),
            JavaScriptTimeout = TimeSpan.FromSeconds(30),
            JavaScriptMaxStatements = 10_000,
            JavaScriptMaxMemoryBytes = 4_000_000
        });

       


        // Domain Event Dispatcher
        services.AddScoped<DomainEventDispatcher>();

        // Transaction Service (handles transactions directly without UoW abstraction)
        services.AddScoped<ITransactionService, TransactionService>();

        // EF Core Repositories (Scoped - one per request/operation, tied to DbContext)
        services.AddScoped<IDeploymentRepository, EfDeploymentRepository>();
        services.AddScoped<IProcessRepository, EfProcessRepository>();
        services.AddScoped<ITokenRepository, EfTokenRepository>();
        services.AddScoped<IIncidentRepository, EfIncidentRepository>();
        services.AddScoped<IBoundarySubscriptionRepository, EfBoundarySubscriptionRepository>();
        services.AddScoped<INodeInstanceRepository, NodeInstanceRepository>();
        services.AddScoped<IUserTaskInstanceRepository, UserTaskInstanceRepository>();
        services.AddScoped<IWorkerRepository, EfWorkerRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IExecutionFlowRepository, ExecutionFlowRepository>();
        // Outbox Services
        services.AddScoped<IOutboxMessageRepository, EfOutboxMessageRepository>();

        services.AddScoped<IDbSeeder, CoreBootstrapSeeder>();
        // Background Services
        services.AddMessageBrockers(configuration);

        // Services
        services.AddScoped<IIncidentService, IncidentService>();

        // Unit of Work (Scoped - one per request/operation)
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        // JSON Serialization Service (Singleton - stateless, can be shared)
        services.AddSingleton<IJsonSerializer, JsonSerializerService>();

        return services;
    }
}
