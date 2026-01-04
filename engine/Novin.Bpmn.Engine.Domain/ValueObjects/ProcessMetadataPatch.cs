// Domain/ValueObjects/ProcessMetadataPatch.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.ValueObjects;

public sealed record ProcessMetadataPatch(
    IReadOnlyDictionary<string, JsonNode?> Upserts,
    IReadOnlyList<string> Removals)
{
    public static readonly ProcessMetadataPatch Empty = ProcessMetadataPatch.From(
        new Dictionary<string, JsonNode?>(),
        Array.Empty<string>());

    public bool HasChanges => Upserts.Count > 0 || Removals.Count > 0;

    public static ProcessMetadataPatch From(
        IDictionary<string, JsonNode?>? upserts,
        IEnumerable<string>? removals)
    {
        var u = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);

        if (upserts != null)
        {
            foreach (var kv in upserts)
            {
                var k = (kv.Key ?? string.Empty).Trim();
                if (k.Length == 0) continue;
                u[k] = JsonVariableCodec.ToNode(kv.Value);
            }
        }

        var r = (removals ?? Array.Empty<string>())
            .Select(x => (x ?? string.Empty).Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();

        return new ProcessMetadataPatch(u, r);
    }
}
