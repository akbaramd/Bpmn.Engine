using System.Collections.ObjectModel;

namespace Novin.Bpmn.Engine.Domain.ValueObjects;

/// <summary>
/// Immutable value object for storing metadata (Tracing/Debug/UI) as key-value pairs.
/// Only for non-hot-path data. Hot-path data should be normalized columns.
/// </summary>
public sealed record MetaBag
{
    private const int MaxKeys = 20;
    private const int MaxKeyLength = 100;
    private const int MaxValueLength = 1000;

    // Allowed keys for domain safety (prevent chaos)
    private static readonly HashSet<string> AllowedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        // Tracing/Debug keys
        "trace.id",
        "trace.parent.id",
        "debug.source",
        "debug.reason",
        "debug.context",
        
        // UI keys
        "ui.label",
        "ui.description",
        "ui.category",
        "ui.priority",
        
        // Custom extension keys (can be extended)
        "custom.*"
    };

    public IReadOnlyDictionary<string, string> Values { get; }

    public bool IsEmpty => Values.Count == 0;

    private MetaBag(IReadOnlyDictionary<string, string> values)
    {
        Values = values;
    }

    public static MetaBag Empty => new(new Dictionary<string, string>());

    public static MetaBag From(IReadOnlyDictionary<string, string>? values)
    {
        if (values is null || values.Count == 0)
            return Empty;

        ValidateKeys(values.Keys);
        ValidateSize(values);
        ValidateLengths(values);

        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in values)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key) || string.IsNullOrWhiteSpace(kvp.Value))
                continue;

            normalized[kvp.Key.Trim()] = kvp.Value.Trim();
        }

        return new MetaBag(new ReadOnlyDictionary<string, string>(normalized));
    }

    public string? Get(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        return Values.TryGetValue(key, out var value) ? value : null;
    }

    public bool Has(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        return Values.ContainsKey(key);
    }

    public MetaBag Set(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or whitespace", nameof(key));

        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be null or whitespace", nameof(value));

        ValidateKey(key);
        ValidateValueLength(value);

        var newValues = Values.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        
        // Check if adding a new key would exceed limit
        if (!newValues.ContainsKey(key) && newValues.Count >= MaxKeys)
            throw new InvalidOperationException($"Cannot add more than {MaxKeys} keys to MetaBag");

        newValues[key.Trim()] = value.Trim();
        ValidateSize(newValues);

        return new MetaBag(new ReadOnlyDictionary<string, string>(newValues));
    }

    public MetaBag Remove(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || !Values.ContainsKey(key))
            return this;

        var newValues = Values.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        newValues.Remove(key);

        return newValues.Count == 0
            ? Empty
            : new MetaBag(new ReadOnlyDictionary<string, string>(newValues));
    }

    public MetaBag Merge(IReadOnlyDictionary<string, string>? additionalValues)
    {
        if (additionalValues is null || additionalValues.Count == 0)
            return this;

        ValidateKeys(additionalValues.Keys);
        ValidateSize(additionalValues);
        ValidateLengths(additionalValues);

        var merged = Values.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        
        foreach (var kvp in additionalValues)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key) || string.IsNullOrWhiteSpace(kvp.Value))
                continue;

            var key = kvp.Key.Trim();
            var value = kvp.Value.Trim();

            if (!merged.ContainsKey(key) && merged.Count >= MaxKeys)
                throw new InvalidOperationException($"Cannot merge: would exceed {MaxKeys} keys limit");

            merged[key] = value;
        }

        return new MetaBag(new ReadOnlyDictionary<string, string>(merged));
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or whitespace", nameof(key));

        if (key.Length > MaxKeyLength)
            throw new ArgumentException($"Key length cannot exceed {MaxKeyLength} characters", nameof(key));

        // Check if key is allowed (exact match or starts with allowed prefix)
        var isAllowed = AllowedKeys.Any(allowed =>
            allowed.Equals(key, StringComparison.OrdinalIgnoreCase) ||
            (allowed.EndsWith(".*", StringComparison.OrdinalIgnoreCase) &&
             key.StartsWith(allowed.Substring(0, allowed.Length - 2), StringComparison.OrdinalIgnoreCase)));

        if (!isAllowed)
            throw new ArgumentException($"Key '{key}' is not in the allowed list. Allowed keys: {string.Join(", ", AllowedKeys)}", nameof(key));
    }

    private static void ValidateKeys(IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            if (!string.IsNullOrWhiteSpace(key))
                ValidateKey(key);
        }
    }

    private static void ValidateValueLength(string value)
    {
        if (value.Length > MaxValueLength)
            throw new ArgumentException($"Value length cannot exceed {MaxValueLength} characters");
    }

    private static void ValidateLengths(IReadOnlyDictionary<string, string> values)
    {
        foreach (var kvp in values)
        {
            if (kvp.Key.Length > MaxKeyLength)
                throw new ArgumentException($"Key '{kvp.Key}' exceeds maximum length of {MaxKeyLength}");

            if (kvp.Value.Length > MaxValueLength)
                throw new ArgumentException($"Value for key '{kvp.Key}' exceeds maximum length of {MaxValueLength}");
        }
    }

    private static void ValidateSize(IReadOnlyDictionary<string, string> values)
    {
        if (values.Count > MaxKeys)
            throw new InvalidOperationException($"MetaBag cannot contain more than {MaxKeys} keys");
    }
}

