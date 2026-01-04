using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Services;

public sealed class BonyanVariables : Dictionary<string, object?>
{
    public BonyanVariables() : base(StringComparer.Ordinal) { }
    public BonyanVariables(int capacity) : base(capacity, StringComparer.Ordinal) { }

    public BonyanVariables(IDictionary<string, object?> dictionary)
        : base(dictionary ?? throw new ArgumentNullException(nameof(dictionary)), StringComparer.Ordinal) { }

    // -------------------------
    // Setters
    // -------------------------

    public void Set(string key, object? value)
    {
        var k = EnsureKey(key);
        this[k] = value;
    }

    public void SetString(string key, string? value) => Set(key, value);
    public void SetInt(string key, int value) => Set(key, value);
    public void SetLong(string key, long value) => Set(key, value);
    public void SetDecimal(string key, decimal value) => Set(key, value);
    public void SetDouble(string key, double value) => Set(key, value);
    public void SetFloat(string key, float value) => Set(key, value);
    public void SetBoolean(string key, bool value) => Set(key, value);
    public void SetDateTime(string key, DateTime value) => Set(key, value.Kind == DateTimeKind.Unspecified ? value : value.ToUniversalTime());
    public void SetGuid(string key, Guid value) => Set(key, value);
    public void SetJson(string key, JsonNode? value) => Set(key, value);

    // -------------------------
    // Getters
    // -------------------------

    public string? GetString(string key, string? fallback = null)
    {
        var k = NormalizeKey(key);
        if (k.Length == 0) return fallback;

        if (!TryGetValue(k, out var v) || v is null) return fallback;

        if (v is string s)
            return string.IsNullOrWhiteSpace(s) ? fallback : s;

        if (v is JsonNode jn)
        {
            try
            {
                var obj = jn.Deserialize<object>(JsonVariableCodec.Options);
                var str = obj?.ToString();
                return string.IsNullOrWhiteSpace(str) ? fallback : str;
            }
            catch
            {
                var raw = jn.ToJsonString(JsonVariableCodec.Options);
                return string.IsNullOrWhiteSpace(raw) ? fallback : raw;
            }
        }

        var txt = v.ToString();
        return string.IsNullOrWhiteSpace(txt) ? fallback : txt;
    }

    public int GetInt(string key, int? fallback = null)
        => TryGetNumber(key, out int v) ? v : (fallback ?? 0);

    public long GetLong(string key, long? fallback = null)
        => TryGetNumber(key, out long v) ? v : (fallback ?? 0L);

    public decimal GetDecimal(string key, decimal? fallback = null)
        => TryGetNumber(key, out decimal v) ? v : (fallback ?? 0m);

    public double GetDouble(string key, double? fallback = null)
        => TryGetNumber(key, out double v) ? v : (fallback ?? 0d);

    public float GetFloat(string key, float? fallback = null)
        => TryGetNumber(key, out float v) ? v : (fallback ?? 0f);

    public bool GetBoolean(string key, bool? fallback = null)
    {
        var k = NormalizeKey(key);
        if (k.Length == 0) return fallback ?? false;

        if (!TryGetValue(k, out var v) || v is null) return fallback ?? false;

        if (v is bool b) return b;

        if (v is JsonNode jn)
        {
            try
            {
                var obj = jn.Deserialize<object>(JsonVariableCodec.Options);
                if (obj is bool bb) return bb;
                if (obj is string s && bool.TryParse(s, out var parsed)) return parsed;
                if (TryLooseBool(obj, out var lb)) return lb;
            }
            catch { }
            return fallback ?? false;
        }

        if (v is string s2)
            return bool.TryParse(s2, out var parsed2) ? parsed2 : (fallback ?? false);

        return TryLooseBool(v, out var loose) ? loose : (fallback ?? false);
    }

    public DateTime GetDateTime(string key, DateTime? fallback = null)
    {
        var k = NormalizeKey(key);
        if (k.Length == 0) return fallback ?? DateTime.MinValue;

        if (!TryGetValue(k, out var v) || v is null) return fallback ?? DateTime.MinValue;

        if (v is DateTime dt) return dt;
        if (v is DateTimeOffset dto) return dto.UtcDateTime;

        if (v is JsonNode jn)
        {
            try
            {
                var obj = jn.Deserialize<object>(JsonVariableCodec.Options);
                if (obj is DateTime dt2) return dt2;
                if (obj is DateTimeOffset dto2) return dto2.UtcDateTime;
                if (obj is string s && DateTime.TryParse(s, null, DateTimeStyles.RoundtripKind, out var parsed))
                    return parsed;
            }
            catch { }
            return fallback ?? DateTime.MinValue;
        }

        if (v is string s2 && DateTime.TryParse(s2, null, DateTimeStyles.RoundtripKind, out var parsed2))
            return parsed2;

        return fallback ?? DateTime.MinValue;
    }

    public Guid GetGuid(string key, Guid? fallback = null)
    {
        var k = NormalizeKey(key);
        if (k.Length == 0) return fallback ?? Guid.Empty;

        if (!TryGetValue(k, out var v) || v is null) return fallback ?? Guid.Empty;

        if (v is Guid g) return g;

        if (v is JsonNode jn)
        {
            try
            {
                var obj = jn.Deserialize<object>(JsonVariableCodec.Options);
                if (obj is Guid g2) return g2;
                if (obj is string s && Guid.TryParse(s, out var parsed)) return parsed;
            }
            catch { }
            return fallback ?? Guid.Empty;
        }

        if (v is string s2 && Guid.TryParse(s2, out var parsed2)) return parsed2;

        return fallback ?? Guid.Empty;
    }

    public bool Has(string key)
    {
        var k = NormalizeKey(key);
        return k.Length != 0 && ContainsKey(k);
    }

    // -------------------------
    // Typed helpers
    // -------------------------

    private bool TryGetNumber<T>(string key, out T value) where T : struct
    {
        value = default;

        var k = NormalizeKey(key);
        if (k.Length == 0) return false;

        if (!TryGetValue(k, out var v) || v is null) return false;

        if (v is T direct)
        {
            value = direct;
            return true;
        }

        if (v is JsonNode jn)
        {
            try
            {
                var obj = jn.Deserialize<object>(JsonVariableCodec.Options);
                return TryConvertNumber(obj, out value);
            }
            catch
            {
                return false;
            }
        }

        return TryConvertNumber(v, out value);
    }

    private static bool TryConvertNumber<T>(object? v, out T value) where T : struct
    {
        value = default;
        if (v is null) return false;

        try
        {
            if (v is T t)
            {
                value = t;
                return true;
            }

            if (v is string s)
            {
                s = s.Trim();
                if (s.Length == 0) return false;

                if (typeof(T) == typeof(int) &&
                    int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var i))
                { value = (T)(object)i; return true; }

                if (typeof(T) == typeof(long) &&
                    long.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var l))
                { value = (T)(object)l; return true; }

                if (typeof(T) == typeof(decimal) &&
                    decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var m))
                { value = (T)(object)m; return true; }

                if (typeof(T) == typeof(double) &&
                    double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                { value = (T)(object)d; return true; }

                if (typeof(T) == typeof(float) &&
                    float.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var f))
                { value = (T)(object)f; return true; }

                return false;
            }

            if (typeof(T) == typeof(int))
            { value = (T)(object)Convert.ToInt32(v, CultureInfo.InvariantCulture); return true; }

            if (typeof(T) == typeof(long))
            { value = (T)(object)Convert.ToInt64(v, CultureInfo.InvariantCulture); return true; }

            if (typeof(T) == typeof(decimal))
            { value = (T)(object)Convert.ToDecimal(v, CultureInfo.InvariantCulture); return true; }

            if (typeof(T) == typeof(double))
            { value = (T)(object)Convert.ToDouble(v, CultureInfo.InvariantCulture); return true; }

            if (typeof(T) == typeof(float))
            { value = (T)(object)Convert.ToSingle(v, CultureInfo.InvariantCulture); return true; }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryLooseBool(object? v, out bool b)
    {
        b = false;
        if (v is null) return false;

        if (v is bool bb) { b = bb; return true; }

        if (v is int i) { b = i != 0; return true; }
        if (v is long l) { b = l != 0; return true; }
        if (v is double d) { b = Math.Abs(d) > double.Epsilon; return true; }
        if (v is float f) { b = Math.Abs(f) > float.Epsilon; return true; }
        if (v is decimal m) { b = m != 0m; return true; }

        if (v is string s)
        {
            s = s.Trim();
            if (bool.TryParse(s, out var parsed)) { b = parsed; return true; }
            if (int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var i2)) { b = i2 != 0; return true; }
        }

        return false;
    }

    private static string NormalizeKey(string key) => (key ?? string.Empty).Trim();

    private static string EnsureKey(string key)
    {
        var k = NormalizeKey(key);
        if (k.Length == 0) throw new ArgumentException("Key cannot be null or empty.", nameof(key));
        return k;
    }
}

public sealed class ScriptExecutionContext
{
    public Guid ProcessId { get; }
    public Guid TokenId { get; }
    public BonyanVariables Variables { get; }

    public ScriptExecutionContext(Guid processId, Guid tokenId, IDictionary<string, object?>? initialVariables = null)
    {
        ProcessId = processId;
        TokenId = tokenId;
        Variables = initialVariables is null ? new BonyanVariables() : new BonyanVariables(initialVariables);
    }
}
