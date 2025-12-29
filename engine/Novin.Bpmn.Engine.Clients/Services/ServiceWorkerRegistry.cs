using Novin.Bpmn.Engine.Clients.Abstractions;

namespace Novin.Bpmn.Engine.Clients.Services;

/// <summary>
/// Implementation of the service worker registry
/// </summary>
public class ServiceWorkerRegistry : IServiceWorkerRegistry
{
    private readonly Dictionary<string, ServiceWorkerConfig> _workers = new();

    /// <summary>
    /// Registers a new service worker
    /// </summary>
    /// <param name="config">The worker configuration</param>
    public void RegisterWorker(ServiceWorkerConfig config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        if (string.IsNullOrWhiteSpace(config.WorkerId))
            throw new ArgumentException("Worker ID cannot be null or empty", nameof(config.WorkerId));

        _workers[config.WorkerId] = config;
        System.Diagnostics.Debug.WriteLine($"Registered worker: {config.WorkerId} with {_workers.Count} total workers");
    }

    /// <summary>
    /// Gets a worker by its ID
    /// </summary>
    /// <param name="workerId">The worker ID</param>
    /// <returns>The worker configuration or null if not found</returns>
    public ServiceWorkerConfig? GetWorker(string workerId)
    {
        if (string.IsNullOrWhiteSpace(workerId))
            return null;

        return _workers.TryGetValue(workerId, out var worker) ? worker : null;
    }

    /// <summary>
    /// Gets all registered workers
    /// </summary>
    /// <returns>Collection of all worker configurations</returns>
    public IEnumerable<ServiceWorkerConfig> GetAllWorkers()
    {
        return _workers.Values.ToList();
    }

    /// <summary>
    /// Gets workers that support a specific work type
    /// Parses workType by splitting on "@" and uses the second part (handler type)
    /// </summary>
    /// <param name="workType">The work type (format: "clientId@handlerType" or "handlerType")</param>
    /// <returns>Collection of workers that support the handler type</returns>
    public IEnumerable<ServiceWorkerConfig> GetWorkersForWorkType(string workType)
    {
        if (string.IsNullOrWhiteSpace(workType))
            return Enumerable.Empty<ServiceWorkerConfig>();

        // Parse workType: split on "@" and use the second part (handler type)
        // Format: "clientId@handlerType:timeout" -> use "handlerType:timeout"
        // Format: "clientId@handlerType" -> use "handlerType"
        // Format: "handlerType" -> use "handlerType"
        string actualWorkType = workType;
        if (workType.Contains("@"))
        {
            var parts = workType.Split('@', 2);
            if (parts.Length > 1)
            {
                actualWorkType = parts[1]; // Use everything after the first "@"
            }
        }

        return _workers.Values
            .Where(w => w.Enabled && w.WorkerId==actualWorkType)
            .ToList();
    }

    /// <summary>
    /// Enables a worker
    /// </summary>
    /// <param name="workerId">The worker ID</param>
    /// <returns>True if the worker was enabled, false if not found</returns>
    public bool EnableWorker(string workerId)
    {
        var worker = GetWorker(workerId);
        if (worker == null)
            return false;

        worker.Enabled = true;
        return true;
    }

    /// <summary>
    /// Disables a worker
    /// </summary>
    /// <param name="workerId">The worker ID</param>
    /// <returns>True if the worker was disabled, false if not found</returns>
    public bool DisableWorker(string workerId)
    {
        var worker = GetWorker(workerId);
        if (worker == null)
            return false;

        worker.Enabled = false;
        return true;
    }

    /// <summary>
    /// Removes a worker from the registry
    /// </summary>
    /// <param name="workerId">The worker ID</param>
    /// <returns>True if the worker was removed, false if not found</returns>
    public bool UnregisterWorker(string workerId)
    {
        return _workers.Remove(workerId);
    }
}