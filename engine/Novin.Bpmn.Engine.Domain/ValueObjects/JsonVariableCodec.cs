// Domain/ValueObjects/JsonVariableCodec.cs
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Novin.Bpmn.Engine.Domain.ValueObjects;

public static class JsonVariableCodec
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public static JsonObject ParseObjectOrEmpty(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new JsonObject();

        try
        {
            var node = JsonNode.Parse(json);
            return node as JsonObject ?? new JsonObject();
        }
        catch
        {
            return new JsonObject();
        }
    }

    public static JsonNode? ToNode(object? value)
    {
        if (value is null) return null;

        // ✅ IMPORTANT: never return the same JsonNode instance (parent issues)
        if (value is JsonNode jn) return CloneNode(jn);

        if (value is string s) return JsonValue.Create(s);
        if (value is bool b) return JsonValue.Create(b);
        if (value is int i) return JsonValue.Create(i);
        if (value is long l) return JsonValue.Create(l);
        if (value is float f) return JsonValue.Create(f);
        if (value is double d) return JsonValue.Create(d);
        if (value is decimal m) return JsonValue.Create(m);
        if (value is Guid g) return JsonValue.Create(g.ToString());
        if (value is DateTime dt) return JsonValue.Create(dt.ToUniversalTime());
        if (value is DateTimeOffset dto) return JsonValue.Create(dto.ToUniversalTime());

        // complex objects
        return JsonSerializer.SerializeToNode(value, Options);
    }

    public static JsonNode? CloneNode(JsonNode? node)
        => node is null ? null : JsonNode.Parse(node.ToJsonString(Options));

    public static string ToStableJson(JsonNode? node)
        => node is null ? "null" : node.ToJsonString(Options);

    public static bool IsValidJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try { using var _ = JsonDocument.Parse(json); return true; }
        catch { return false; }
    }

    // ✅ FIX: clone nodes before inserting into JsonObject
    public static string SerializeVars(Dictionary<string, JsonNode?>? vars)
    {
        var obj = new JsonObject();

        if (vars is not null)
        {
            foreach (var kv in vars)
            {
                if (string.IsNullOrWhiteSpace(kv.Key))
                    continue;

                // 🔥 key point: clone to avoid "already has a parent"
                obj[kv.Key] = CloneNode(kv.Value);
            }
        }

        return obj.ToJsonString(Options);
    }

    // ✅ FIX: detach nodes from parsed parent by cloning
    public static Dictionary<string, JsonNode?> DeserializeVars(string? json)
    {
        var dict = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json)) return dict;

        var node = JsonNode.Parse(json);
        if (node is not JsonObject obj) return dict;

        foreach (var kv in obj)
        {
            if (!string.IsNullOrWhiteSpace(kv.Key))
                dict[kv.Key] = CloneNode(kv.Value);
        }

        return dict;
    }

    public static string Sha256Hex(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);

        var hex = new char[hash.Length * 2];
        var j = 0;
        for (int i = 0; i < hash.Length; i++)
        {
            var b = hash[i];
            hex[j++] = (char)((b >> 4) < 10 ? ('0' + (b >> 4)) : ('a' + ((b >> 4) - 10)));
            hex[j++] = (char)((b & 0xF) < 10 ? ('0' + (b & 0xF)) : ('a' + ((b & 0xF) - 10)));
        }
        return new string(hex);
    }
}
