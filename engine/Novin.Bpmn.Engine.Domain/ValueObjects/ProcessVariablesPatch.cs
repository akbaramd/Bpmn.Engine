using System.Collections.ObjectModel;

namespace Novin.Bpmn.Engine.Domain.ValueObjects;

/// <summary>
/// Immutable patch describing variable upserts (add/update) and removals.
/// </summary>
public sealed class ProcessVariablesPatch
{
    public IReadOnlyDictionary<string, string> Upserts { get; }
    public IReadOnlyCollection<string> Removals { get; }

    public bool HasChanges => Upserts.Count > 0 || Removals.Count > 0;

    private ProcessVariablesPatch(
        IDictionary<string, string> upserts,
        ICollection<string> removals)
    {
        Upserts = new ReadOnlyDictionary<string, string>(upserts);
        Removals = new ReadOnlyCollection<string>(removals.ToList());
    }

    public static ProcessVariablesPatch From(
        IDictionary<string, object?>? upserts,
        IEnumerable<string>? removals)
    {
        var normalizedUpserts = new Dictionary<string, string>(StringComparer.Ordinal);
        if (upserts is not null)
        {
            foreach (var pair in upserts)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                    throw new ArgumentException("Variable key cannot be null or whitespace.", nameof(upserts));

                normalizedUpserts[pair.Key] = ConvertToString(pair.Value);
            }
        }

        var normalizedRemovals = new HashSet<string>(StringComparer.Ordinal);
        if (removals is not null)
        {
            foreach (var key in removals)
            {
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                normalizedRemovals.Add(key);
            }
        }

        // If a key is upserted, remove it from removals to avoid conflicting intentions
        foreach (var upsertKey in normalizedUpserts.Keys)
        {
            normalizedRemovals.Remove(upsertKey);
        }

        return new ProcessVariablesPatch(normalizedUpserts, normalizedRemovals);
    }

    private static string ConvertToString(object? value)
    {
        if (value is null) return string.Empty;
        if (value is string str) return str;

        return Newtonsoft.Json.JsonConvert.SerializeObject(value);
    }
}

