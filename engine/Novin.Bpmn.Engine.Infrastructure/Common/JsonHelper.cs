using Newtonsoft.Json;

namespace Novin.Bpmn.Engine.Infrastructure.Common;

/// <summary>
/// Static helper for JSON serialization in contexts where dependency injection is not available
/// (e.g., EF Core value converters configured during model building).
/// Uses JsonConvert directly for simplicity.
/// </summary>
internal static class JsonHelper
{
    /// <summary>
    /// Serializes an object to JSON string
    /// </summary>
    public static string SerializeObject(object? value)
    {
        if (value == null)
        {
            return string.Empty;
        }

        return JsonConvert.SerializeObject(value);
    }

    /// <summary>
    /// Deserializes a JSON string to an object of the specified type
    /// </summary>
    public static T? DeserializeObject<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default(T);
        }

        return JsonConvert.DeserializeObject<T>(json);
    }

    /// <summary>
    /// Deserializes a JSON string to an object of the specified type.
    /// This is the non-generic method that works with Type objects at runtime.
    /// </summary>
    /// <param name="json">The JSON string to deserialize</param>
    /// <param name="type">The target type (must not be null)</param>
    /// <returns>The deserialized object, or null if json is null or empty</returns>
    /// <exception cref="ArgumentNullException">Thrown when type is null</exception>
    public static object? DeserializeObject(string? json, Type type)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        return JsonConvert.DeserializeObject(json, type);
    }
}
