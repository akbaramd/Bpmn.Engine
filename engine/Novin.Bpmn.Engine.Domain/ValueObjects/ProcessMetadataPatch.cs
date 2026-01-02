using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Novin.Bpmn.Engine.Domain.ValueObjects;

public sealed class ProcessMetadataPatch
{
    public IReadOnlyDictionary<string, string> Upserts { get; }
    public IReadOnlyCollection<string> Removals { get; }
    public bool HasChanges => Upserts.Count > 0 || Removals.Count > 0;

    private ProcessMetadataPatch(
        IReadOnlyDictionary<string, string> upserts,
        IReadOnlyCollection<string> removals)
    {
        Upserts = upserts;
        Removals = removals;
    }

    public static ProcessMetadataPatch From(
        IDictionary<string, object?> incoming,
        IEnumerable<string> removals)
    {
        var upserts = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (k, v) in incoming)
        {
            if (string.IsNullOrWhiteSpace(k)) continue;

            var key = k.Trim();

            // Convention: null => removal (optional; you can remove this if you want strictness)
            if (v is null)
                continue;

            // store as stable string
            var str = v switch
            {
                string s => s,
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
                _ => v.ToString() ?? string.Empty
            };

            if (string.IsNullOrWhiteSpace(str))
                continue; // treat empty as "no upsert" (removal handled separately)

            upserts[key] = str;
        }

        var rem = removals
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();

        return new ProcessMetadataPatch(upserts, rem);
    }

    public static ProcessMetadataPatch Empty()
        => new ProcessMetadataPatch(
            new Dictionary<string, string>(StringComparer.Ordinal),
            Array.Empty<string>());
}
