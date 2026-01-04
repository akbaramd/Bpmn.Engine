using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Novin.Bpmn.Engine.Application.Services;

internal static class FeelEval
{
    public static object? Eval(FeelNode n, IReadOnlyDictionary<string, object?> vars)
        => n switch
        {
            FeelLiteral lit   => lit.Value,
            FeelIdentifier id => Resolve(id.Name, vars),
            FeelUnary un      => EvalUnary(un.Op, Eval(un.Expr, vars)),
            FeelBinary bin    => EvalBinary(bin.Op, Eval(bin.Left, vars), Eval(bin.Right, vars)),
            _ => throw new InvalidOperationException("Unknown AST node.")
        };

    // -------------------------
    // Unwrap (JsonNode/JsonElement -> CLR)
    // -------------------------
    private static object? Unwrap(object? v)
    {
        if (v is null) return null;

        // JsonNode -> primitive/dict/list
        if (v is JsonNode jn)
            return FeelVarConvert.ToDotNet(jn);

        // JsonElement -> primitive/raw
        if (v is JsonElement je)
            return FeelVarConvert.FromJsonElement(je);

        return v;
    }

    // -------------------------
    // Bool coercion
    // -------------------------
    public static bool AsBool(object? v)
    {
        v = Unwrap(v);

        return v switch
        {
            bool b => b,
            null => false,

            // treat "false"/"" as false, everything else non-empty true
            string s => !string.IsNullOrWhiteSpace(s)
                        && !string.Equals(s, "false", StringComparison.OrdinalIgnoreCase),

            double d => Math.Abs(d) > double.Epsilon,
            float f => Math.Abs(f) > float.Epsilon,
            decimal m => m != 0m,
            long l => l != 0,
            int i => i != 0,

            _ => true
        };
    }

    // -------------------------
    // Variable resolution
    // -------------------------
    private static object? Resolve(string name, IReadOnlyDictionary<string, object?> vars)
        => vars.TryGetValue(name, out var v) ? v : null;

    // -------------------------
    // Unary
    // -------------------------
    private static object? EvalUnary(string op, object? v)
        => op switch
        {
            "not" => !AsBool(v),
            _ => throw new InvalidOperationException($"Unsupported unary operator '{op}'.")
        };

    // -------------------------
    // Binary
    // -------------------------
    private static object? EvalBinary(string op, object? a, object? b)
        => op switch
        {
            "and" => AsBool(a) && AsBool(b),
            "or" => AsBool(a) || AsBool(b),

            "=" => EqualsLoose(a, b),
            "!=" => !EqualsLoose(a, b),

            ">" => Compare(a, b) > 0,
            ">=" => Compare(a, b) >= 0,
            "<" => Compare(a, b) < 0,
            "<=" => Compare(a, b) <= 0,

            _ => throw new InvalidOperationException($"Unsupported operator '{op}'.")
        };

    // -------------------------
    // Equality (loose)
    // -------------------------
    private static bool EqualsLoose(object? a, object? b)
    {
        a = Unwrap(a);
        b = Unwrap(b);

        if (a is null && b is null) return true;
        if (a is null || b is null) return false;

        // number compare
        if (TryNumber(a, out var da) && TryNumber(b, out var db))
            return Math.Abs(da - db) < 1e-9;

        // bool compare (important for flag=true)
        if (a is bool ba && b is bool bb)
            return ba == bb;

        // string compare
        if (a is string sa && b is string sb)
            return string.Equals(sa, sb, StringComparison.Ordinal);

        return a.Equals(b);
    }

    // -------------------------
    // Comparison
    // -------------------------
    private static int Compare(object? a, object? b)
    {
        a = Unwrap(a);
        b = Unwrap(b);

        if (a is null && b is null) return 0;
        if (a is null) return -1;
        if (b is null) return 1;

        if (TryNumber(a, out var da) && TryNumber(b, out var db))
            return da.CompareTo(db);

        if (a is string sa && b is string sb)
            return string.Compare(sa, sb, StringComparison.Ordinal);

        var aStr = a.ToString();
        var bStr = b.ToString();
        return string.Compare(aStr, bStr, StringComparison.Ordinal);
    }

    // -------------------------
    // Numeric coercion
    // -------------------------
    private static bool TryNumber(object? v, out double d)
    {
        v = Unwrap(v);

        switch (v)
        {
            case double dd: d = dd; return true;
            case float ff: d = ff; return true;
            case decimal dec: d = (double)dec; return true;
            case long ll: d = ll; return true;
            case int ii: d = ii; return true;
            case short ss: d = ss; return true;
            case byte bb: d = bb; return true;

            case string s when double.TryParse(
                s,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var parsed):
                d = parsed;
                return true;

            default:
                d = 0;
                return false;
        }
    }
}
