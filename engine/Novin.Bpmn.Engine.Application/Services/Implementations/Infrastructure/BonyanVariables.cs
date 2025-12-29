namespace Novin.Bpmn.Engine.Application.Services;

/// <summary>
/// BonyanVariables - A dictionary that extends Dictionary&lt;string, string&gt; with typed setter and getter methods.
/// All values are stored as strings internally, but provides convenient methods for different types.
/// </summary>
public sealed class BonyanVariables : Dictionary<string, string>
{
    /// <summary>
    /// Initializes a new instance of BonyanVariables
    /// </summary>
    public BonyanVariables() : base()
    {
    }

    /// <summary>
    /// Initializes a new instance of BonyanVariables with initial capacity
    /// </summary>
    public BonyanVariables(int capacity) : base(capacity)
    {
    }

    /// <summary>
    /// Initializes a new instance of BonyanVariables with initial values
    /// </summary>
    public BonyanVariables(IDictionary<string, string> dictionary) : base(dictionary)
    {
    }

    /// <summary>
    /// Sets a variable as a string value
    /// </summary>
    public void SetString(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        this[key] = value ?? string.Empty;
    }

    /// <summary>
    /// Sets a variable as an integer value (converted to string)
    /// </summary>
    public void SetInt(string key, int value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        this[key] = value.ToString();
    }

    /// <summary>
    /// Sets a variable as a long value (converted to string)
    /// </summary>
    public void SetLong(string key, long value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        this[key] = value.ToString();
    }

    /// <summary>
    /// Sets a variable as a decimal value (converted to string)
    /// </summary>
    public void SetDecimal(string key, decimal value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        this[key] = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Sets a variable as a double value (converted to string)
    /// </summary>
    public void SetDouble(string key, double value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        this[key] = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Sets a variable as a float value (converted to string)
    /// </summary>
    public void SetFloat(string key, float value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        this[key] = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Sets a variable as a boolean value (converted to string)
    /// </summary>
    public void SetBoolean(string key, bool value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        this[key] = value.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// Sets a variable as a DateTime value (converted to ISO 8601 string)
    /// </summary>
    public void SetDateTime(string key, DateTime value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        this[key] = value.ToString("O"); // ISO 8601 format
    }

    /// <summary>
    /// Sets a variable as a Guid value (converted to string)
    /// </summary>
    public void SetGuid(string key, Guid value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        this[key] = value.ToString();
    }

    /// <summary>
    /// Gets a variable value as string
    /// </summary>
    /// <param name="key">The variable key</param>
    /// <param name="fallback">Fallback value if key doesn't exist or is null/empty</param>
    /// <returns>The string value or fallback if key doesn't exist or is null/empty</returns>
    public string? GetString(string key, string? fallback = null)
    {
        if (!TryGetValue(key, out var value) || string.IsNullOrEmpty(value))
            return fallback;

        return value;
    }

    /// <summary>
    /// Gets a variable value as integer (parsed from string)
    /// </summary>
    /// <param name="key">The variable key</param>
    /// <param name="fallback">Fallback value if key doesn't exist or parsing fails</param>
    /// <returns>The integer value or fallback if key doesn't exist or parsing fails</returns>
    public int GetInt(string key, int? fallback = null)
    {
        if (!TryGetValue(key, out var value) || string.IsNullOrEmpty(value))
            return fallback ?? 0;

        return int.TryParse(value, out var result) ? result : (fallback ?? 0);
    }

    /// <summary>
    /// Gets a variable value as long (parsed from string)
    /// </summary>
    /// <param name="key">The variable key</param>
    /// <param name="fallback">Fallback value if key doesn't exist or parsing fails</param>
    /// <returns>The long value or fallback if key doesn't exist or parsing fails</returns>
    public long GetLong(string key, long? fallback = null)
    {
        if (!TryGetValue(key, out var value) || string.IsNullOrEmpty(value))
            return fallback ?? 0L;

        return long.TryParse(value, out var result) ? result : (fallback ?? 0L);
    }

    /// <summary>
    /// Gets a variable value as decimal (parsed from string)
    /// </summary>
    /// <param name="key">The variable key</param>
    /// <param name="fallback">Fallback value if key doesn't exist or parsing fails</param>
    /// <returns>The decimal value or fallback if key doesn't exist or parsing fails</returns>
    public decimal GetDecimal(string key, decimal? fallback = null)
    {
        if (!TryGetValue(key, out var value) || string.IsNullOrEmpty(value))
            return fallback ?? 0m;

        return decimal.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var result) 
            ? result 
            : (fallback ?? 0m);
    }

    /// <summary>
    /// Gets a variable value as double (parsed from string)
    /// </summary>
    /// <param name="key">The variable key</param>
    /// <param name="fallback">Fallback value if key doesn't exist or parsing fails</param>
    /// <returns>The double value or fallback if key doesn't exist or parsing fails</returns>
    public double GetDouble(string key, double? fallback = null)
    {
        if (!TryGetValue(key, out var value) || string.IsNullOrEmpty(value))
            return fallback ?? 0.0;

        return double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var result) 
            ? result 
            : (fallback ?? 0.0);
    }

    /// <summary>
    /// Gets a variable value as float (parsed from string)
    /// </summary>
    /// <param name="key">The variable key</param>
    /// <param name="fallback">Fallback value if key doesn't exist or parsing fails</param>
    /// <returns>The float value or fallback if key doesn't exist or parsing fails</returns>
    public float GetFloat(string key, float? fallback = null)
    {
        if (!TryGetValue(key, out var value) || string.IsNullOrEmpty(value))
            return fallback ?? 0f;

        return float.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var result) 
            ? result 
            : (fallback ?? 0f);
    }

    /// <summary>
    /// Gets a variable value as boolean (parsed from string)
    /// </summary>
    /// <param name="key">The variable key</param>
    /// <param name="fallback">Fallback value if key doesn't exist or parsing fails</param>
    /// <returns>The boolean value or fallback if key doesn't exist or parsing fails</returns>
    public bool GetBoolean(string key, bool? fallback = null)
    {
        if (!TryGetValue(key, out var value) || string.IsNullOrEmpty(value))
            return fallback ?? false;

        return bool.TryParse(value, out var result) ? result : (fallback ?? false);
    }

    /// <summary>
    /// Gets a variable value as DateTime (parsed from ISO 8601 string)
    /// </summary>
    /// <param name="key">The variable key</param>
    /// <param name="fallback">Fallback value if key doesn't exist or parsing fails</param>
    /// <returns>The DateTime value or fallback if key doesn't exist or parsing fails</returns>
    public DateTime GetDateTime(string key, DateTime? fallback = null)
    {
        if (!TryGetValue(key, out var value) || string.IsNullOrEmpty(value))
            return fallback ?? DateTime.MinValue;

        return DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var result) 
            ? result 
            : (fallback ?? DateTime.MinValue);
    }

    /// <summary>
    /// Gets a variable value as Guid (parsed from string)
    /// </summary>
    /// <param name="key">The variable key</param>
    /// <param name="fallback">Fallback value if key doesn't exist or parsing fails</param>
    /// <returns>The Guid value or fallback if key doesn't exist or parsing fails</returns>
    public Guid GetGuid(string key, Guid? fallback = null)
    {
        if (!TryGetValue(key, out var value) || string.IsNullOrEmpty(value))
            return fallback ?? Guid.Empty;

        return Guid.TryParse(value, out var result) ? result : (fallback ?? Guid.Empty);
    }

    /// <summary>
    /// Checks if a variable exists
    /// </summary>
    public bool Has(string key)
    {
        return ContainsKey(key);
    }
}
