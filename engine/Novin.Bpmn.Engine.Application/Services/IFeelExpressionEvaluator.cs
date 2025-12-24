using System.Globalization;

namespace Novin.Bpmn.Engine.Application.Services.Feel;

// -------------------------
// Public API

public interface IFeelExpressionEvaluator
{
    object? Evaluate(string expression, IReadOnlyDictionary<string, object?> vars);
    bool EvaluateBoolean(string expression, IReadOnlyDictionary<string, object?> vars);
}

public sealed class FeelExpressionEvaluator : IFeelExpressionEvaluator
{
    public object? Evaluate(string expression, IReadOnlyDictionary<string, object?> vars)
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

    public bool EvaluateBoolean(string expression, IReadOnlyDictionary<string, object?> vars)
    {
        var value = Evaluate(expression, vars);
        return FeelEval.AsBool(value);
    }

    public bool TryEvaluate(
        string expression,
        IReadOnlyDictionary<string, object?> variables,
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

        // Zeebe-style / mapping-style: "=customerId"
        if (expr.StartsWith("=", StringComparison.Ordinal))
            expr = expr.Substring(1).Trim();

        return expr;
    }
}

// -------------------------
// Lexer
// -------------------------
internal enum FeelTokenKind
{
    Identifier,
    Number,
    String,
    True,
    False,
    Null,

    LParen,
    RParen,

    Eq,      // =
    Neq,     // !=
    Gt,      // >
    Gte,     // >=
    Lt,      // <
    Lte,     // <=

    And,     // and
    Or,      // or
    Not,     // not

    End
}

internal readonly record struct FeelToken(FeelTokenKind Kind, string Text);

internal sealed class FeelLexer
{
    private readonly string _s;
    private int _i;

    public FeelLexer(string s) => _s = s;

    public List<FeelToken> Tokenize()
    {
        var list = new List<FeelToken>();
        while (true)
        {
            SkipWs();
            if (_i >= _s.Length) { list.Add(new FeelToken(FeelTokenKind.End, "")); return list; }

            var c = _s[_i];

            // punctuation
            if (c == '(') { _i++; list.Add(new FeelToken(FeelTokenKind.LParen, "(")); continue; }
            if (c == ')') { _i++; list.Add(new FeelToken(FeelTokenKind.RParen, ")")); continue; }

            // operators
            if (c == '=')
            {
                _i++;
                list.Add(new FeelToken(FeelTokenKind.Eq, "="));
                continue;
            }

            if (c == '!' && Peek('='))
            {
                _i += 2;
                list.Add(new FeelToken(FeelTokenKind.Neq, "!="));
                continue;
            }

            if (c == '>' && Peek('='))
            {
                _i += 2;
                list.Add(new FeelToken(FeelTokenKind.Gte, ">="));
                continue;
            }
            if (c == '<' && Peek('='))
            {
                _i += 2;
                list.Add(new FeelToken(FeelTokenKind.Lte, "<="));
                continue;
            }
            if (c == '>')
            {
                _i++;
                list.Add(new FeelToken(FeelTokenKind.Gt, ">"));
                continue;
            }
            if (c == '<')
            {
                _i++;
                list.Add(new FeelToken(FeelTokenKind.Lt, "<"));
                continue;
            }

            // string literal: "VIP"
            if (c == '"')
            {
                _i++;
                var start = _i;
                while (_i < _s.Length && _s[_i] != '"') _i++;
                if (_i >= _s.Length) throw new InvalidOperationException("Unterminated string literal.");
                var str = _s.Substring(start, _i - start);
                _i++; // closing "
                list.Add(new FeelToken(FeelTokenKind.String, str));
                continue;
            }

            // number
            if (char.IsDigit(c) || (c == '-' && _i + 1 < _s.Length && char.IsDigit(_s[_i + 1])))
            {
                var start = _i;
                _i++;
                while (_i < _s.Length && (char.IsDigit(_s[_i]) || _s[_i] == '.')) _i++;
                list.Add(new FeelToken(FeelTokenKind.Number, _s.Substring(start, _i - start)));
                continue;
            }

            // identifier / keyword
            if (char.IsLetter(c) || c == '_')
            {
                var start = _i;
                _i++;
                while (_i < _s.Length && (char.IsLetterOrDigit(_s[_i]) || _s[_i] == '_' || _s[_i] == '.')) _i++;
                var text = _s.Substring(start, _i - start);

                var k = text.ToLowerInvariant() switch
                {
                    "and" => FeelTokenKind.And,
                    "or" => FeelTokenKind.Or,
                    "not" => FeelTokenKind.Not,
                    "true" => FeelTokenKind.True,
                    "false" => FeelTokenKind.False,
                    "null" => FeelTokenKind.Null,
                    _ => FeelTokenKind.Identifier
                };
                list.Add(new FeelToken(k, text));
                continue;
            }

            throw new InvalidOperationException($"Unexpected character '{c}' at position {_i}.");
        }
    }

    private bool Peek(char ch) => _i + 1 < _s.Length && _s[_i + 1] == ch;

    private void SkipWs()
    {
        while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
    }
}

// -------------------------
// Parser (precedence: not > comparisons > and > or)
// -------------------------
internal abstract record FeelNode;

internal sealed record FeelLiteral(object? Value) : FeelNode;
internal sealed record FeelIdentifier(string Name) : FeelNode;
internal sealed record FeelUnary(string Op, FeelNode Expr) : FeelNode;
internal sealed record FeelBinary(string Op, FeelNode Left, FeelNode Right) : FeelNode;

internal sealed class FeelParser
{
    private readonly List<FeelToken> _t;
    private int _p;

    public FeelParser(List<FeelToken> tokens) => _t = tokens;

    public FeelNode ParseExpression() => ParseOr();

    private FeelNode ParseOr()
    {
        var left = ParseAnd();
        while (Match(FeelTokenKind.Or))
        {
            var right = ParseAnd();
            left = new FeelBinary("or", left, right);
        }
        return left;
    }

    private FeelNode ParseAnd()
    {
        var left = ParseNot();
        while (Match(FeelTokenKind.And))
        {
            var right = ParseNot();
            left = new FeelBinary("and", left, right);
        }
        return left;
    }

    private FeelNode ParseNot()
    {
        if (Match(FeelTokenKind.Not))
        {
            var expr = ParseNot();
            return new FeelUnary("not", expr);
        }
        return ParseComparison();
    }

    private FeelNode ParseComparison()
    {
        var left = ParsePrimary();

        if (Match(FeelTokenKind.Eq)) return new FeelBinary("=", left, ParsePrimary());
        if (Match(FeelTokenKind.Neq)) return new FeelBinary("!=", left, ParsePrimary());
        if (Match(FeelTokenKind.Gte)) return new FeelBinary(">=", left, ParsePrimary());
        if (Match(FeelTokenKind.Lte)) return new FeelBinary("<=", left, ParsePrimary());
        if (Match(FeelTokenKind.Gt)) return new FeelBinary(">", left, ParsePrimary());
        if (Match(FeelTokenKind.Lt)) return new FeelBinary("<", left, ParsePrimary());

        return left;
    }

    private FeelNode ParsePrimary()
    {
        var tok = Current;

        if (Match(FeelTokenKind.LParen))
        {
            var e = ParseExpression();
            Expect(FeelTokenKind.RParen);
            return e;
        }

        if (Match(FeelTokenKind.True)) return new FeelLiteral(true);
        if (Match(FeelTokenKind.False)) return new FeelLiteral(false);
        if (Match(FeelTokenKind.Null)) return new FeelLiteral(null);

        if (Match(FeelTokenKind.Number))
        {
            if (!double.TryParse(tok.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                throw new InvalidOperationException($"Invalid number '{tok.Text}'.");
            return new FeelLiteral(d);
        }

        if (Match(FeelTokenKind.String))
            return new FeelLiteral(tok.Text);

        if (Match(FeelTokenKind.Identifier))
            return new FeelIdentifier(tok.Text);

        throw new InvalidOperationException($"Unexpected token {tok.Kind} ('{tok.Text}').");
    }

    public void Expect(FeelTokenKind kind)
    {
        if (Current.Kind != kind)
            throw new InvalidOperationException($"Expected {kind} but got {Current.Kind} ('{Current.Text}').");
        _p++;
    }

    private bool Match(FeelTokenKind kind)
    {
        if (Current.Kind != kind) return false;
        _p++;
        return true;
    }

    private FeelToken Current => _t[_p];
}

// -------------------------
// Evaluator
// -------------------------
internal static class FeelEval
{
    public static object? Eval(FeelNode n, IReadOnlyDictionary<string, object?> vars)
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

    private static object? Resolve(string name, IReadOnlyDictionary<string, object?> vars)
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
