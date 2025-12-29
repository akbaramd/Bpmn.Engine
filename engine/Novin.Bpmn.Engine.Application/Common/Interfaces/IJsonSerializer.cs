namespace Novin.Bpmn.Engine.Application.Common.Interfaces;

/// <summary>
/// Centralized JSON serialization service interface.
/// Provides consistent JSON serialization/deserialization across the application.
/// Supports both generic and non-generic (Type-based) serialization/deserialization.
/// </summary>
public interface IJsonSerializer
{
    /// <summary>
    /// Serializes an object to JSON string using standardized settings.
    /// Works with any object type at runtime.
    /// </summary>
    /// <param name="value">The object to serialize (can be null)</param>
    /// <returns>JSON string representation, or empty string if value is null</returns>
    string SerializeObject(object? value);

    /// <summary>
    /// Deserializes a JSON string to an object of the specified generic type.
    /// Use this method when the type is known at compile time.
    /// </summary>
    /// <typeparam name="T">The target type (known at compile time)</typeparam>
    /// <param name="json">The JSON string to deserialize</param>
    /// <returns>The deserialized object, or default(T) if json is null or empty</returns>
    T? DeserializeObject<T>(string? json);

    /// <summary>
    /// Deserializes a JSON string to an object of the specified type.
    /// Use this method when the type is only known at runtime (e.g., from Type.GetType()).
    /// This is the non-generic method that works with Type objects.
    /// </summary>
    /// <param name="json">The JSON string to deserialize</param>
    /// <param name="type">The target type (must not be null)</param>
    /// <returns>The deserialized object, or null if json is null or empty</returns>
    /// <exception cref="ArgumentNullException">Thrown when type is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when deserialization fails</exception>
    object? DeserializeObject(string? json, Type type);
}
