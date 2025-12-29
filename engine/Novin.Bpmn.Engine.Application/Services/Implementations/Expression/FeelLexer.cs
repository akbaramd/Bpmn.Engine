namespace Novin.Bpmn.Engine.Application.Services;

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