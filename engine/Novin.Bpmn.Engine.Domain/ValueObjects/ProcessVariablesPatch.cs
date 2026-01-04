// Domain/ValueObjects/VariablesPatch.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Novin.Bpmn.Engine.Domain.ValueObjects;

public record VariablesPatch(
    IReadOnlyDictionary<string, JsonNode?> Upserts,
    IReadOnlyList<string> Removals)
{
    public bool HasChanges => (Upserts?.Count ?? 0) > 0 || (Removals?.Count ?? 0) > 0;

    public static VariablesPatch Empty { get; } =
        new VariablesPatch(new Dictionary<string, JsonNode?>(StringComparer.Ordinal), Array.Empty<string>());

 public static VariablesPatch UpsertAllFromNodes(IReadOnlyDictionary<string, JsonNode?> raw)
    {
        if (raw is null || raw.Count == 0) return Empty;

        var upserts = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var kv in raw)
        {
            var k = kv.Key?.Trim();
            if (string.IsNullOrWhiteSpace(k)) continue;
            upserts[k] = kv.Value; // already JsonNode
        }

        return new VariablesPatch(upserts, Array.Empty<string>());
    }

    public static VariablesPatch From(IDictionary<string, object?>? upserts, IEnumerable<string>? removals)
    {
        var u = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);

        if (upserts is not null)
        {
            foreach (var kv in upserts)
            {
                var key = NormalizeKey(kv.Key);
                if (string.IsNullOrWhiteSpace(key)) continue;

                u[key] = JsonVariableCodec.ToNode(kv.Value);
            }
        }

        var r = removals is null
            ? Array.Empty<string>()
            : removals.Select(NormalizeKey)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

        return new VariablesPatch(u, r);
    }

    public static string NormalizeKey(string? key)
        => string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
}

// Backward compatible name (optional)
public sealed record ProcessVariablesPatch(
    IReadOnlyDictionary<string, JsonNode?> Upserts,
    IReadOnlyList<string> Removals)
    : VariablesPatch(Upserts, Removals)
{
    public static new ProcessVariablesPatch From(IDictionary<string, object?>? upserts, IEnumerable<string>? removals)
    {
        var p = VariablesPatch.From(upserts, removals);
        return new ProcessVariablesPatch(p.Upserts, p.Removals);
    }

     
}
