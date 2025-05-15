namespace Novin.Bpmn.EventSourcing.Feel
{
    public sealed class Parser
    {
        private readonly IReadOnlyList<Token> _tokens;
        private int _current;

        public Parser(IReadOnlyList<Token> tokens)
        {
            _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
            _current = 0;
        }

        /// <summary>
        /// شروع پارس کردن و ساخت AST از روی توکن‌ها
        /// </summary>
        public IExpression Parse() => Expression();

        private IExpression Expression()
        {
            return LogicOr();
        }

        private IExpression LogicOr()
        {
            var expr = LogicAnd();
            while (Match(TokenType.Or))
                expr = new BinaryExpr(expr, Previous(), LogicAnd());
            return expr;
        }

        private IExpression LogicAnd()
        {
            var expr = Equality();
            while (Match(TokenType.And))
                expr = new BinaryExpr(expr, Previous(), Equality());
            return expr;
        }

        private IExpression Equality()
        {
            var expr = Comparison();
            while (Match(TokenType.Equals, TokenType.NotEquals))
                expr = new BinaryExpr(expr, Previous(), Comparison());
            return expr;
        }

        private IExpression Comparison()
        {
            var expr = Term();

            while (Match(TokenType.Greater, TokenType.GreaterEq, TokenType.Less, TokenType.LessEq,
                         TokenType.In, TokenType.Between))
            {
                var op = Previous();

                if (op.Type == TokenType.Between)
                {
                    var min = Term();
                    Consume(TokenType.And, "Expected 'and' after 'between'");
                    var max = Term();
                    var range = new RangeExpr(min, true, max, true);
                    expr = new BinaryExpr(expr, new Token(TokenType.In, "in", op.Line, op.Column), range);
                }
                else
                {
                    expr = new BinaryExpr(expr, op, Term());
                }
            }

            return expr;
        }

        private IExpression Term()
        {
            var expr = Factor();
            while (Match(TokenType.Plus, TokenType.Minus))
                expr = new BinaryExpr(expr, Previous(), Factor());
            return expr;
        }

        private IExpression Power()
        {
            var expr = Unary();
            while (Match(TokenType.Power))
                expr = new BinaryExpr(expr, Previous(), Unary());
            return expr;
        }

        private IExpression Factor()
        {
            var expr = Power();
            while (Match(TokenType.Star, TokenType.Slash, TokenType.Percent))
                expr = new BinaryExpr(expr, Previous(), Unary());
            return expr;
        }

        private IExpression Unary()
        {
            if (Match(TokenType.Not, TokenType.Minus))
                return new UnaryExpr(Previous(), Unary());

            return Primary();
        }

        private IExpression Primary()
        {
            // پشتیبانی از شرطی در هر جای عبارت
            if (Match(TokenType.If))
            {
                var condition = Expression();
                Consume(TokenType.Then, "Expected 'then' after condition");
                var thenExpr = Expression();
                Consume(TokenType.Else, "Expected 'else' after 'then' branch");
                var elseExpr = Expression();
                return new ConditionalExpr(condition, thenExpr, elseExpr);
            }

            if (Match(TokenType.Boolean))
                return new LiteralExpr(bool.Parse(Previous().Lexeme.ToLower()));

            if (Match(TokenType.Null))
                return new LiteralExpr(null);

            if (Match(TokenType.Number))
            {
                var number = new LiteralExpr(decimal.Parse(Previous().Lexeme, System.Globalization.CultureInfo.InvariantCulture));
                
                if (Match(TokenType.Range) && Check(TokenType.Number))
                {
                    var end = Term();
                    return new RangeExpr(number, true, end, true);
                }
                
                return number;
            }

            if (Match(TokenType.String))
                return new LiteralExpr(Previous().Lexeme);

            if (Match(TokenType.Identifier))
            {
                var id = Previous();
                if (Match(TokenType.LParen))
                {
                    var args = new List<IExpression>();
                    if (!Check(TokenType.RParen))
                    {
                        do args.Add(Expression());
                        while (Match(TokenType.Comma));
                    }
                    Consume(TokenType.RParen, "Expected ')' after arguments");
                    return new FuncCallExpr(id.Lexeme, args);
                }

                return new IdentifierExpr(id.Lexeme);
            }

            if (Match(TokenType.LParen))
            {
                var expr = Expression();
                Consume(TokenType.RParen, "Expected ')' after expression");
                return expr;
            }

            if (Match(TokenType.LBracket)) // لیست
            {
                var elements = new List<IExpression>();
                if (!Check(TokenType.RBracket))
                {
                    do elements.Add(Expression());
                    while (Match(TokenType.Comma));
                }
                Consume(TokenType.RBracket, "Expected ']' after list");
                return new ListExpr(elements);
            }

            if (Match(TokenType.LBrace)) // کانتکست
            {
                var entries = new Dictionary<string, IExpression>();
                if (!Check(TokenType.RBrace))
                {
                    do
                    {
                        var key = Consume(TokenType.Identifier, "Expected identifier as key").Lexeme;
                        Consume(TokenType.Colon, "Expected ':' after key");
                        var value = Expression();
                        entries[key] = value;
                    }
                    while (Match(TokenType.Comma));
                }
                Consume(TokenType.RBrace, "Expected '}' after context");
                return new ContextExpr(entries);
            }

            var token = Peek();
            throw new FeelParseException($"Unexpected token '{token.Lexeme}' at line {token.Line}, column {token.Column}");
        }

        // متدهای کمکی

        private bool Match(params TokenType[] types)
        {
            foreach (var type in types)
            {
                if (Check(type))
                {
                    Advance();
                    return true;
                }
            }

            return false;
        }

        private Token Consume(TokenType type, string message)
        {
            if (Check(type)) return Advance();

            var token = Peek();
            throw new FeelParseException($"{message} at line {token.Line}, column {token.Column}");
        }

        private bool Check(TokenType type) => !IsAtEnd() && Peek().Type == type;

        private Token Advance() => !IsAtEnd() ? _tokens[_current++] : _tokens[^1];

        private bool IsAtEnd() => Peek().Type == TokenType.EoF;

        private Token Peek() => _tokens[_current];

        private Token Previous() => _tokens[_current - 1];
    }
}
