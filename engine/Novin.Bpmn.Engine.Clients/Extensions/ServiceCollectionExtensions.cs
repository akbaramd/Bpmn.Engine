using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Clients.Abstractions;
using Novin.Bpmn.Engine.Clients.Services;

namespace Novin.Bpmn.Engine.Clients.Extensions;

/// <summary>
/// Extension methods for configuring BPMN engine client services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds BPMN engine client services to the dependency injection container
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="clientId">Unique identifier for this client</param>
    /// <param name="webhookBaseUrl">Base URL for webhook callbacks</param>
    /// <param name="configureOptions">Optional action to configure client options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddBpmnEngineClient(
        this IServiceCollection services,
        string clientId,
        string engineBaseUrl,
        Action<BpmnClientOptions>? configureOptions = null)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("Client ID cannot be null or empty", nameof(clientId));

        if (string.IsNullOrWhiteSpace(engineBaseUrl))
            throw new ArgumentException("Engine base URL cannot be null or empty", nameof(engineBaseUrl));

        // Configure and validate options
        var options = new BpmnClientOptions();
        try
        {
            options.ClientId = clientId;
            options.EngineBaseUrl = engineBaseUrl;
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException($"Invalid BPMN client configuration: {ex.Message}", ex);
        }

        configureOptions?.Invoke(options);

        services.AddSingleton(options);

        // Register core services
        services.AddSingleton<IBpmnClientService, BpmnClientService>();
        services.AddSingleton<IServiceWorkerRegistry>(sp =>
        {
            var registry = new ServiceWorkerRegistry();
            var workerConfigs = sp.GetServices<ServiceWorkerConfig>();
            foreach (var config in workerConfigs)
            {
                registry.RegisterWorker(config);
            }
            return registry;
        });
        services.AddSingleton<IClientConnectionManager, ClientConnectionManager>();
        services.AddSingleton<ISignalRClientService, SignalRClientService>();

        // Register SignalR client for engine communication
        services.AddSignalR();

        // Register background service for auto-connection and work processing
        services.AddHostedService<BpmnClientBackgroundService>();

        // Register logging
        services.AddLogging();

        return services;
    }

    /// <summary>
    /// Adds a service worker to the BPMN engine client
    /// </summary>
    /// <typeparam name="THandler">The handler type that implements BpmnServiceWorkerHandler</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="configureWorker">Optional action to configure the worker</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddServiceWorker<THandler>(
        this IServiceCollection services,
        Action<ServiceWorkerConfig>? configureWorker = null)
        where THandler : BpmnWorkerHandler
    {
        // Get worker metadata from attribute
        var handlerType = typeof(THandler);
        var workerAttribute = Attribute.GetCustomAttribute(handlerType, typeof(BpmnWorkerAttribute)) as BpmnWorkerAttribute;

        if (workerAttribute == null)
        {
            throw new InvalidOperationException($"Handler {handlerType.Name} must have a BpmnWorker attribute");
        }

        var config = new ServiceWorkerConfig
        {
            WorkerId = workerAttribute.WorkerId,
            Name = workerAttribute.Name ?? workerAttribute.WorkerId,
            Description = workerAttribute.Description,
            HandlerType = handlerType,
            SupportedWorkTypes = new List<string> { workerAttribute.WorkType }
        };

        configureWorker?.Invoke(config);

        // Register the handler as transient
        services.AddTransient(typeof(THandler));

        // Register the worker configuration (will be picked up by the registry factory)
        services.AddSingleton(config);

        return services;
    }

    /// <summary>
    /// Adds a service worker to the BPMN engine client with multiple work types
    /// </summary>
    /// <typeparam name="THandler">The handler type that implements BpmnServiceWorkerHandler</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="workTypes">List of additional work types this worker can handle</param>
    /// <param name="configureWorker">Optional action to configure the worker</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddServiceWorker<THandler>(
        this IServiceCollection services,
        IEnumerable<string> workTypes,
        Action<ServiceWorkerConfig>? configureWorker = null)
        where THandler : BpmnWorkerHandler
    {
        if (workTypes == null || !workTypes.Any())
            throw new ArgumentException("Work types cannot be null or empty", nameof(workTypes));

        // Get worker metadata from attribute
        var handlerType = typeof(THandler);
        var workerAttribute = Attribute.GetCustomAttribute(handlerType, typeof(BpmnWorkerAttribute)) as BpmnWorkerAttribute;

        if (workerAttribute == null)
        {
            throw new InvalidOperationException($"Handler {handlerType.Name} must have a BpmnWorker attribute");
        }

        var allWorkTypes = new List<string> { workerAttribute.WorkType };
        allWorkTypes.AddRange(workTypes);

        var config = new ServiceWorkerConfig
        {
            WorkerId = workerAttribute.WorkerId,
            Name = workerAttribute.Name ?? workerAttribute.WorkerId,
            Description = workerAttribute.Description,
            HandlerType = handlerType,
            SupportedWorkTypes = allWorkTypes
        };

        configureWorker?.Invoke(config);

        // Register the handler as transient
        services.AddTransient(typeof(THandler));

        // Register the worker configuration (will be picked up by the registry factory)
        services.AddSingleton(config);

        return services;
    }
}