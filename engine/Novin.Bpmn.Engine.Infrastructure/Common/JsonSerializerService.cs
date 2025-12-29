using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Novin.Bpmn.Engine.Application.Common.Interfaces;

namespace Novin.Bpmn.Engine.Infrastructure.Common;

/// <summary>
/// Centralized JSON serialization service implementation using Newtonsoft.Json.
/// Provides consistent JSON serialization/deserialization with standardized settings.
/// </summary>
public sealed class JsonSerializerService : IJsonSerializer
{
    private readonly ILogger<JsonSerializerService> _logger;

    public JsonSerializerService(ILogger<JsonSerializerService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string SerializeObject(object? value)
    {
        if (value == null)
        {
            return string.Empty;
        }

        try
        {
            return JsonConvert.SerializeObject(value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to serialize object of type {Type}", value.GetType().FullName);
            throw new InvalidOperationException($"Failed to serialize object of type {value.GetType().FullName}", ex);
        }
    }

    /// <inheritdoc />
    public T? DeserializeObject<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default(T);
        }

        try
        {
            return JsonConvert.DeserializeObject<T>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize JSON to type {Type}. JSON: {Json}", typeof(T).FullName, json);
            throw new InvalidOperationException($"Failed to deserialize JSON to type {typeof(T).FullName}", ex);
        }
    }

    /// <inheritdoc />
    public object? DeserializeObject(string? json, Type type)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        try
        {
            return JsonConvert.DeserializeObject(json, type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize JSON to type {Type}. JSON: {Json}", type.FullName, json);
            throw new InvalidOperationException($"Failed to deserialize JSON to type {type.FullName}", ex);
        }
    }
}
