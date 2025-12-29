using Novin.Bpmn.Engine.Clients.Abstractions;

namespace Novin.Bpmn.Engine.Clients.Services;

/// <summary>
/// Registry for managing service workers
/// </summary>
public interface IServiceWorkerRegistry
{
    /// <summary>
    /// Registers a new service worker
    /// </summary>
    /// <param name="config">The worker configuration</param>
    void RegisterWorker(ServiceWorkerConfig config);

    /// <summary>
    /// Gets a worker by its ID
    /// </summary>
    /// <param name="workerId">The worker ID</param>
    /// <returns>The worker configuration or null if not found</returns>
    ServiceWorkerConfig? GetWorker(string workerId);

    /// <summary>
    /// Gets all registered workers
    /// </summary>
    /// <returns>Collection of all worker configurations</returns>
    IEnumerable<ServiceWorkerConfig> GetAllWorkers();

    /// <summary>
    /// Gets workers that support a specific work type
    /// </summary>
    /// <param name="workType">The work type</param>
    /// <returns>Collection of workers that support the work type</returns>
    IEnumerable<ServiceWorkerConfig> GetWorkersForWorkType(string workType);

    /// <summary>
    /// Enables a worker
    /// </summary>
    /// <param name="workerId">The worker ID</param>
    /// <returns>True if the worker was enabled, false if not found</returns>
    bool EnableWorker(string workerId);

    /// <summary>
    /// Disables a worker
    /// </summary>
    /// <param name="workerId">The worker ID</param>
    /// <returns>True if the worker was disabled, false if not found</returns>
    bool DisableWorker(string workerId);

    /// <summary>
    /// Removes a worker from the registry
    /// </summary>
    /// <param name="workerId">The worker ID</param>
    /// <returns>True if the worker was removed, false if not found</returns>
    bool UnregisterWorker(string workerId);
}