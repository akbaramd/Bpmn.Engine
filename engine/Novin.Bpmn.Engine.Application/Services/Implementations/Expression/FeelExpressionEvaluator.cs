namespace Novin.Bpmn.Engine.Application.Services;

public sealed class FeelExpressionEvaluator : IFeelExpressionEvaluator
{
    public object? Evaluate(string expression, IReadOnlyDictionary<string, string?> vars)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return null;

        expression = Normalize(expression);

        var lexer = new FeelLexer(expression);
        var tokens = lexer.Tokenize();
        var parser = new FeelParser(tokens);
        var ast = parser.ParseExpression();
        parser.Expect(FeelTokenKind.End);

        return FeelEval.Eval(ast, vars);
    }

    public bool EvaluateBoolean(string expression, IReadOnlyDictionary<string, string?> vars)
    {
        var value = Evaluate(expression, vars);
        return FeelEval.AsBool(value);
    }

    public bool TryEvaluate(
        string expression,
        IReadOnlyDictionary<string, string?> variables,
        out bool result,
        out string? error)
    {
        result = false;
        error = null;

        if (string.IsNullOrWhiteSpace(expression))
        {
            error = "Expression is empty.";
            return false;
        }

        try
        {
            result = EvaluateBoolean(expression, variables);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string Normalize(string expr)
    {
        expr = expr.Trim();

        // 1) Zeebe-style / mapping-style marker: "= <feel>"
        if (expr.StartsWith("=", StringComparison.Ordinal))
            expr = expr.Substring(1).TrimStart();

        // 2) Allow C-like equality as alias -> FEEL uses "=" for equality
        //    This prevents token stream: Eq Eq ( "==" ) from breaking the parser.
        //    IMPORTANT: only replace "==" (NOT ">=" or "<=" or "!=").
        if (expr.Contains("==", StringComparison.Ordinal))
            expr = expr.Replace("==", "=", StringComparison.Ordinal);

        return expr;
    }
}
