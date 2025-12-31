// Infrastructure/Persistence/Ef/EfComparers.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Novin.Bpmn.Engine.Infrastructure.Common;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Ef;

/// <summary>
/// EF Core ValueComparer helpers for JSON-mapped collections/dictionaries.
/// Keep them deterministic (ordering) and snapshot-friendly.
/// </summary>
internal static class EfComparers
{
    // -----------------------------
    // HashSet<Guid> (TokenIds / NodeInstanceIds)
    // -----------------------------
    public static bool GuidSetEqual(HashSet<Guid>? a, HashSet<Guid>? b)
        => ReferenceEquals(a, b) || (a is not null && b is not null && a.SetEquals(b));

    public static int GuidSetHash(HashSet<Guid>? v)
    {
        if (v is null || v.Count == 0) return 0;

        // deterministic aggregation
        var hash = 0;
        foreach (var g in v.OrderBy(x => x))
            hash = HashCode.Combine(hash, g.GetHashCode());

        return hash;
    }

    public static HashSet<Guid> GuidSetSnapshot(HashSet<Guid>? v)
        => v is null ? new HashSet<Guid>() : new HashSet<Guid>(v);

    // -----------------------------
    // Dictionary<string,string> (Process._variables)
    // -----------------------------
    public static bool VarsEqual(Dictionary<string, string>? a, Dictionary<string, string>? b)
        => ReferenceEquals(a, b) || JsonHelper.SerializeObject(a) == JsonHelper.SerializeObject(b);

    public static int VarsHash(Dictionary<string, string>? v)
        => v is null ? 0 : JsonHelper.SerializeObject(v).GetHashCode();

    public static Dictionary<string, string> VarsSnapshot(Dictionary<string, string>? v)
        => v is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(v, StringComparer.Ordinal);

    // -----------------------------
    // IReadOnlyDictionary<string,string> (Token.Variables)
    // -----------------------------
    public static bool ReadOnlyDictEqual(IReadOnlyDictionary<string, string>? a, IReadOnlyDictionary<string, string>? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.Count != b.Count) return false;

        return JsonHelper.SerializeObject(a) == JsonHelper.SerializeObject(b);
    }

    public static int ReadOnlyDictHash(IReadOnlyDictionary<string, string>? v)
        => v is null ? 0 : JsonHelper.SerializeObject(v).GetHashCode();

    public static IReadOnlyDictionary<string, string> ReadOnlyDictSnapshot(IReadOnlyDictionary<string, string>? v)
        => v is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(v, StringComparer.Ordinal);

    // -----------------------------
    // List<Guid> (Token._parentTokenIds)
    // -----------------------------
    public static bool GuidListEqual(List<Guid>? a, List<Guid>? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.Count != b.Count) return false;
        return a.SequenceEqual(b);
    }

    public static int GuidListHash(List<Guid>? v)
    {
        if (v is null || v.Count == 0) return 0;

        var hash = 0;
        foreach (var g in v)
            hash = HashCode.Combine(hash, g.GetHashCode());

        return hash;
    }

    public static List<Guid> GuidListSnapshot(List<Guid>? v)
        => v is null ? new List<Guid>() : new List<Guid>(v);

    // -----------------------------
    // List<string> (NodeInstance._arrivedViaFlowIds)
    // -----------------------------
    public static bool ListEqual(List<string>? a, List<string>? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.Count != b.Count) return false;
        return a.SequenceEqual(b, StringComparer.Ordinal);
    }

    public static int ListHash(List<string>? v)
    {
        if (v is null || v.Count == 0) return 0;

        var hash = 0;
        foreach (var s in v)
            hash = HashCode.Combine(hash, s?.GetHashCode() ?? 0);

        return hash;
    }

    public static List<string> ListSnapshot(List<string>? v)
        => v is null ? new List<string>() : new List<string>(v);
}
