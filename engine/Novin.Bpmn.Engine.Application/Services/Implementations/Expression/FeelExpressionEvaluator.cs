using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Services;




public sealed class FeelExpressionEvaluator : IFeelExpressionEvaluator
{
    public object? Evaluate(string expression, IReadOnlyDictionary<string, JsonNode?> vars)
        => EvaluateInternal(expression, FeelVarConvert.JsonNodesToObjects(vars));

    public bool EvaluateBoolean(string expression, IReadOnlyDictionary<string, JsonNode?> vars)
        => FeelEval.AsBool(Evaluate(expression, vars));

    public object? Evaluate(string expression, IReadOnlyDictionary<string, object?> vars)
        => EvaluateInternal(expression, vars);

    public bool EvaluateBoolean(string expression, IReadOnlyDictionary<string, object?> vars)
        => FeelEval.AsBool(Evaluate(expression, vars));

    private static object? EvaluateInternal(string expression, IReadOnlyDictionary<string, object?> vars)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return null;

        if(!expression.StartsWith("="))  {
            return expression;
        }

        var normalized = Normalize(expression);

        var lexer = new FeelLexer(normalized);
        var tokens = lexer.Tokenize();
        var parser = new FeelParser(tokens);
        var ast = parser.ParseExpression();
        parser.Expect(FeelTokenKind.End);

        return FeelEval.Eval(ast, vars);
    }

    private static string Normalize(string expr)
    {
        expr = expr.Trim();

        if (expr.StartsWith("=", StringComparison.Ordinal))
            expr = expr.Substring(1).TrimStart();

        // FEEL equality is "=" , but allow C# style "=="
        if (expr.Contains("==", StringComparison.Ordinal))
            expr = expr.Replace("==", "=", StringComparison.Ordinal);

        return expr;
    }
}

internal static class FeelVarConvert
{
    public static IReadOnlyDictionary<string, object?> JsonNodesToObjects(IReadOnlyDictionary<string, JsonNode?> vars)
    {
        if (vars.Count == 0) return new Dictionary<string, object?>(StringComparer.Ordinal);

        var dict = new Dictionary<string, object?>(vars.Count, StringComparer.Ordinal);
        foreach (var kv in vars)
        {
            var key = (kv.Key ?? string.Empty).Trim();
            if (key.Length == 0) continue;

            dict[key] = ToDotNet(kv.Value);
        }

        return dict;
    }

    // ✅ مهم: JsonValue را به bool/number/string واقعی تبدیل می‌کند (نه JsonElement)
    public static object? ToDotNet(JsonNode? node)
    {
        if (node is null) return null;

        if (node is JsonValue jv)
        {
            if (jv.TryGetValue<bool>(out var b)) return b;

            if (jv.TryGetValue<long>(out var l)) return l;
            if (jv.TryGetValue<int>(out var i)) return i;
            if (jv.TryGetValue<double>(out var d)) return d;
            if (jv.TryGetValue<decimal>(out var m)) return m;
            if (jv.TryGetValue<string>(out var s)) return s;

            // fallback (rare): unwrap JsonElement if it exists
            try
            {
                var je = jv.GetValue<JsonElement>();
                return FromJsonElement(je);
            }
            catch
            {
                return jv.ToJsonString(JsonVariableCodec.Options);
            }
        }

        if (node is JsonObject obj)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var kv in obj)
            {
                var k = (kv.Key ?? string.Empty).Trim();
                if (k.Length == 0) continue;

                dict[k] = ToDotNet(kv.Value);
            }
            return dict;
        }

        if (node is JsonArray arr)
        {
            var list = new List<object?>(arr.Count);
            foreach (var item in arr)
                list.Add(ToDotNet(item));
            return list;
        }

        return node.ToJsonString(JsonVariableCodec.Options);
    }

    public static JsonNode? ToJsonNode(object? value)
    {
        if (value is null) return null;

        // ✅ clone to avoid "node already has a parent"
        if (value is JsonNode jn) return JsonVariableCodec.CloneNode(jn);

        // اگر JsonElement آمد (مثلا از بیرون)، به JsonNode تبدیلش کن
        if (value is JsonElement je) return JsonNode.Parse(je.GetRawText());

        return JsonVariableCodec.ToNode(value);
    }

    public static object? FromJsonElement(JsonElement e) =>
        e.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => e.TryGetInt64(out var l) ? l :
                                    e.TryGetDouble(out var d) ? d : e.GetRawText(),
            JsonValueKind.String => e.GetString(),
            JsonValueKind.Null => null,
            _ => e.GetRawText()
        };
}