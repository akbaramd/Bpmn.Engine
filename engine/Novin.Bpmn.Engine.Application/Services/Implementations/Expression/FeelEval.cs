using System.Globalization;

namespace Novin.Bpmn.Engine.Application.Services;

internal static class FeelEval
{
    public static object? Eval(FeelNode n, IReadOnlyDictionary<string, string?> vars)
        => n switch
        {
            FeelLiteral lit => lit.Value,
            FeelIdentifier id => Resolve(id.Name, vars),
            FeelUnary un => EvalUnary(un.Op, Eval(un.Expr, vars)),
            FeelBinary bin => EvalBinary(bin.Op, Eval(bin.Left, vars), Eval(bin.Right, vars)),
            _ => throw new InvalidOperationException("Unknown AST node.")
        };

    public static bool AsBool(object? v)
        => v switch
        {
            bool b => b,
            null => false,
            string s => !string.IsNullOrWhiteSpace(s),
            double d => Math.Abs(d) > double.Epsilon,
            long l => l != 0,
            int i => i != 0,
            _ => true
        };

    private static object? Resolve(string name, IReadOnlyDictionary<string, string?> vars)
        => vars.TryGetValue(name, out var v) ? v : null;

    private static object? EvalUnary(string op, object? v)
        => op switch
        {
            "not" => !AsBool(v),
            _ => throw new InvalidOperationException($"Unsupported unary operator '{op}'.")
        };

    private static object? EvalBinary(string op, object? a, object? b)
    {
        return op switch
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
    }

    private static bool EqualsLoose(object? a, object? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;

        // number compare
        if (TryNumber(a, out var da) && TryNumber(b, out var db))
            return Math.Abs(da - db) < 1e-9;

        // string compare
        if (a is string sa && b is string sb)
            return string.Equals(sa, sb, StringComparison.Ordinal);

        return a.Equals(b);
    }

    private static int Compare(object? a, object? b)
    {
        if (TryNumber(a, out var da) && TryNumber(b, out var db))
            return da.CompareTo(db);

        var sa = a?.ToString();
        var sb = b?.ToString();
        return string.Compare(sa, sb, StringComparison.Ordinal);
    }

    private static bool TryNumber(object? v, out double d)
    {
        switch (v)
        {
            case double dd: d = dd; return true;
            case float ff: d = ff; return true;
            case long ll: d = ll; return true;
            case int ii: d = ii; return true;
            case decimal dec: d = (double)dec; return true;
            case string s when double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed):
                d = parsed; return true;
            default:
                d = 0;
                return false;
        }
    }
}