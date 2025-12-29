using System.Threading.Tasks;
using Novin.Bpmn.Engine.Application.Services;

namespace Novin.Bpmn.Engine.Clients.Abstractions;

/// <summary>
/// Abstract base class for BPMN service worker handlers
/// </summary>
public abstract class BpmnWorkerHandler
{
    /// <summary>
    /// The unique identifier for this handler
    /// </summary>
    public abstract string HandlerId { get; }

    /// <summary>
    /// The type of work this handler can process
    /// </summary>
    public abstract string WorkType { get; }

    /// <summary>
    /// Execute the work asynchronously
    /// </summary>
    /// <param name="workerContext">The worker context containing execution data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public abstract Task ExecuteAsync(WorkerContext workerContext, CancellationToken cancellationToken = default);
}

/// <summary>
/// Context representing a worker execution environment, similar to the Worker entity
/// All fields are strings, Variables use BonyanVariables
/// </summary>
public class WorkerContext
{
    /// <summary>
    /// Unique identifier for the worker
    /// </summary>
    public Guid WorkerId { get; set; }

    /// <summary>
    /// Process instance ID (as string)
    /// </summary>
    public string ProcessId { get; set; } = string.Empty;

    /// <summary>
    /// Token ID (as string)
    /// </summary>
    public string TokenId { get; set; } = string.Empty;

    /// <summary>
    /// BPMN element ID
    /// </summary>
    public string ElementId { get; set; } = string.Empty;

    /// <summary>
    /// Task name
    /// </summary>
    public string TaskName { get; set; } = string.Empty;

    /// <summary>
    /// Worker implementation type
    /// </summary>
    public string Implementation { get; set; } = string.Empty;

    /// <summary>
    /// Worker type (comes from the worker configuration)
    /// </summary>
    public string WorkerType { get; set; } = string.Empty;

    /// <summary>
    /// Worker metadata (assignee, priority, due date, etc.) - all values as strings
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Worker variables/result data - uses BonyanVariables with typed setter/getter methods
    /// </summary>
    public BonyanVariables Variables { get; set; } = new();

    /// <summary>
    /// Adds a variable to the worker context (converts value to string)
    /// </summary>
    /// <param name="name">Variable name</param>
    /// <param name="value">Variable value (converted to string)</param>
    public void AddVariable(string name, object? value)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Variable name cannot be null or empty", nameof(name));

        Variables.SetString(name, ConvertToString(value));
    }

    /// <summary>
    /// Adds metadata to the worker context (converts value to string)
    /// </summary>
    /// <param name="key">Metadata key</param>
    /// <param name="value">Metadata value (converted to string)</param>
    public void SetMeta(string key, object? value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Metadata key cannot be null or empty", nameof(key));

        Metadata[key] = ConvertToString(value);
    }

    /// <summary>
    /// Gets a variable by name (returns as string)
    /// </summary>
    /// <param name="name">Variable name</param>
    /// <returns>Variable value as string or null if not found</returns>
    public string? GetVariable(string name)
    {
        return Variables.GetString(name);
    }

    /// <summary>
    /// Gets metadata by key (returns as string)
    /// </summary>
    /// <param name="key">Metadata key</param>
    /// <returns>Metadata value as string or null if not found</returns>
    public string? GetMetadata(string key)
    {
        return Metadata.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// Converts an object to string representation
    /// </summary>
    private static string ConvertToString(object? value)
    {
        if (value == null)
            return string.Empty;

        if (value is string str)
            return str;

        // Use JSON serialization for complex types
        return Newtonsoft.Json.JsonConvert.SerializeObject(value);
    }
}