using System.Text.Json.Nodes;

namespace Novin.Bpmn.Engine.Application.Services;

// -------------------------
// Public API

public interface IFeelExpressionEvaluator
{
    object? Evaluate(string expression, IReadOnlyDictionary<string, JsonNode?> vars);
    bool EvaluateBoolean(string expression, IReadOnlyDictionary<string, JsonNode?> vars);

    // optional: برای جاهایی مثل ScriptContext که object دارند
    object? Evaluate(string expression, IReadOnlyDictionary<string, object?> vars);
    bool EvaluateBoolean(string expression, IReadOnlyDictionary<string, object?> vars);
}