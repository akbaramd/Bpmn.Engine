namespace Novin.Bpmn.Engine.Application.Services;

// -------------------------
// Public API

public interface IFeelExpressionEvaluator
{
    object? Evaluate(string expression, IReadOnlyDictionary<string, string?> vars);
    bool EvaluateBoolean(string expression, IReadOnlyDictionary<string, string?> vars);
}

// -------------------------
// Lexer
// -------------------------

// -------------------------
// Parser (precedence: not > comparisons > and > or)
// -------------------------

// -------------------------
// Evaluator
// -------------------------