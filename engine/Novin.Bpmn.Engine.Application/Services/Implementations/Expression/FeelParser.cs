using System.Globalization;

namespace Novin.Bpmn.Engine.Application.Services;

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