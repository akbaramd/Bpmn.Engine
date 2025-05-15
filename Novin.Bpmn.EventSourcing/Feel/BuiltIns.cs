using System.Collections;

namespace Novin.Bpmn.EventSourcing.Feel;

public static class BuiltIns
{
    private static readonly Dictionary<string, Func<object?[], object?>> _functions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["sum"] = args => CheckList(args, 0).Sum(Convert.ToDecimal),
            ["count"] = args => CheckList(args, 0).Count(),
            ["min"] = args => CheckList(args, 0).Min(),
            ["max"] = args => CheckList(args, 0).Max(),
            ["substring"] = args =>
            {
                var str = args[0]?.ToString() ?? "";
                var start = Convert.ToInt32(args[1]);
                var len = args.Length > 2 ? Convert.ToInt32(args[2]) : str.Length - start;
                return str.Substring(start, len);
            },
            ["lower case"] = args => args[0]?.ToString()?.ToLowerInvariant(),
            ["upper case"] = args => args[0]?.ToString()?.ToUpperInvariant(),
            ["now"] = _ => DateTime.UtcNow,
        };

    public static bool TryInvoke(string functionName, object?[] args, out object? result)
    {
        if (_functions.TryGetValue(functionName, out var func))
        {
            result = func(args);
            return true;
        }

        result = null;
        return false;
    }

    private static IEnumerable<object?> CheckList(object?[] args, int index)
    {
        if (args.Length <= index)
            throw new ArgumentException("Missing list argument");

        if (args[index] is IEnumerable enumerable and not string)
            return enumerable.Cast<object?>();

        throw new ArgumentException($"Argument {index} must be a list");
    }
}
