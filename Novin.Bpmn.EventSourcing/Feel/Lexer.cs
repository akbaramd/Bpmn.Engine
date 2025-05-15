using System.Globalization;

namespace Novin.Bpmn.EventSourcing.Feel
{
    /// <summary>
    /// کلاس Lexer وظیفهٔ تبدیل رشته ورودی FEEL به توکن‌ها (Token Stream) را بر عهده دارد.
    /// این توکن‌ها برای ساختن AST در Parser استفاده می‌شوند.
    /// </summary>
    public sealed class Lexer
    {
        private readonly string _source;
        private readonly List<Token> _tokens = new();

        private int _start = 0;     // اندیس شروع توکن جاری
        private int _current = 0;   // اندیس جاری در رشته ورودی
        private int _line = 1;      // شماره خط (برای خطاگیری بهتر)
        private int _column = 1;    // شماره ستون (برای خطاگیری بهتر)

        // کلمات کلیدی FEEL با حساسیت به حروف کم و زیاد (Case-Insensitive)
        private static readonly Dictionary<string, TokenType> _keywords = new(StringComparer.OrdinalIgnoreCase)
        {
            ["and"]     = TokenType.And,
            ["or"]      = TokenType.Or,
            ["not"]     = TokenType.Not,
            ["in"]      = TokenType.In,
            ["if"]      = TokenType.If,
            ["then"]    = TokenType.Then,
            ["else"]    = TokenType.Else,
            ["between"] = TokenType.Between,
            ["true"]    = TokenType.Boolean,
            ["false"]   = TokenType.Boolean,
            ["null"]    = TokenType.Null
        };

        public Lexer(string source) => _source = source ?? throw new ArgumentNullException(nameof(source));

        /// <summary>
        /// شروع فرایند توکن‌سازی: رشته ورودی را به لیست توکن تبدیل می‌کند.
        /// </summary>
        public IReadOnlyList<Token> ScanTokens()
        {
            while (!IsAtEnd())
            {
                _start = _current;
                ScanToken();
            }

            // افزودن توکن انتهای ورودی (EOF)
            _tokens.Add(new Token(TokenType.EoF, string.Empty, _line, _column));
            return _tokens;
        }

        /// <summary>
        /// خواندن کاراکتر بعدی و تبدیل به توکن بر اساس نوع آن
        /// </summary>
        private void ScanToken()
        {
            char c = Advance();

            switch (c)
            {
                case '+': Add(TokenType.Plus); break;
                case '-': Add(TokenType.Minus); break;
                case '*': Add(TokenType.Star); break;
                case '/': Add(TokenType.Slash); break;
                case '%': Add(TokenType.Percent); break;
                case '(': Add(TokenType.LParen); break;
                case ')': Add(TokenType.RParen); break;
                case '[': Add(TokenType.LBracket); break;
                case ']': Add(TokenType.RBracket); break;
                case '{': Add(TokenType.LBrace); break;
                case '}': Add(TokenType.RBrace); break;
                case ',': Add(TokenType.Comma); break;
                case '^': Add(TokenType.Power); break;
                case ':': Add(TokenType.Colon); break; // اضافه شده برای پشتیبانی context key:value
                case '.':
                    if (Match('.'))
                        Add(TokenType.Range);
                    else
                        Add(TokenType.Dot);
                    break;
                case '=':
                    if (Match('='))
                        Add(TokenType.Equals);
                    else
                        Add(TokenType.Equals); // در FEEL، تک '=' نیز برای مقایسه است
                    break;
                case '!':
                    Add(Match('=') ? TokenType.NotEquals : TokenType.Not);
                    break;
                case '>':
                    Add(Match('=') ? TokenType.GreaterEq : TokenType.Greater);
                    break;
                case '<':
                    Add(Match('=') ? TokenType.LessEq : TokenType.Less);
                    break;
                case '"':
                    ScanString();
                    break;
                case ' ':
                case '\t':
                case '\r':
                    // فضاهای سفید را نادیده می‌گیریم ولی ستون را افزایش می‌دهیم
                    _column++;
                    break;
                case '\n':
                    _line++;
                    _column = 1;
                    break;
                default:
                    if (char.IsDigit(c))
                        ScanNumber();
                    else if (IsAlpha(c))
                        ScanIdentifier();
                    else
                        throw new FeelLexException($"Unexpected character '{c}' at line {_line}, column {_column}");
                    break;
            }
        }

        /// <summary>
        /// خواندن کاراکتر بعدی و افزایش اندیس‌ها
        /// </summary>
        private char Advance()
        {
            char c = _source[_current++];
            _column++;
            return c;
        }

        /// <summary>
        /// بررسی اینکه آیا کاراکتر بعدی برابر با مقدار مورد نظر است و در صورت درست بودن پیشروی می‌کند.
        /// </summary>
        private bool Match(char expected)
        {
            if (IsAtEnd()) return false;
            if (_source[_current] != expected) return false;

            _current++;
            _column++;
            return true;
        }

        /// <summary>
        /// افزودن توکن جدید با نوع مشخص و Lexeme از متن ورودی
        /// </summary>
        private void Add(TokenType type)
        {
            string lexeme = _source[_start.._current];
            _tokens.Add(new Token(type, lexeme, _line, _column - ( _current - _start )));
        }

        /// <summary>
        /// بررسی رسیدن به انتهای رشته ورودی
        /// </summary>
        private bool IsAtEnd() => _current >= _source.Length;

        /// <summary>
        /// اسکن رشته متنی درون "..."
        /// </summary>
        private void ScanString()
        {
            while (!IsAtEnd() && Peek() != '"')
            {
                if (Peek() == '\n')
                {
                    _line++;
                    _column = 1;
                }
                Advance();
            }

            if (IsAtEnd())
                throw new FeelLexException($"Unterminated string at line {_line}, column {_column}");

            // consume closing quote
            Advance();

            string value = _source[(_start + 1)..(_current - 1)];
            _tokens.Add(new Token(TokenType.String, value, _line, _column - ( _current - _start )));
        }

        /// <summary>
        /// اسکن عدد (عدد صحیح یا اعشاری)
        /// </summary>
        private void ScanNumber()
        {
            while (!IsAtEnd() && char.IsDigit(Peek()))
                Advance();

            // پشتیبانی از اعشار
            if (!IsAtEnd() && Peek() == '.' && char.IsDigit(PeekNext()))
            {
                Advance(); // consume dot
                while (!IsAtEnd() && char.IsDigit(Peek()))
                    Advance();
            }

            string text = _source[_start.._current];
            if (!decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                throw new FeelLexException($"Invalid number format '{text}' at line {_line}, column {_column}");

            _tokens.Add(new Token(TokenType.Number, text, _line, _column - ( _current - _start )));
        }

        /// <summary>
        /// اسکن شناسه (نام متغیر یا کلمه کلیدی)
        /// </summary>
        private void ScanIdentifier()
        {
            while (!IsAtEnd() && (char.IsLetterOrDigit(Peek()) || Peek() == '_'))
                Advance();

            string text = _source[_start.._current];
            if (_keywords.TryGetValue(text, out var type))
                _tokens.Add(new Token(type, text, _line, _column - ( _current - _start )));
            else
                _tokens.Add(new Token(TokenType.Identifier, text, _line, _column - ( _current - _start )));
        }

        /// <summary>
        /// نگاه کردن به کاراکتر جاری بدون مصرف آن
        /// </summary>
        private char Peek() => IsAtEnd() ? '\0' : _source[_current];

        /// <summary>
        /// نگاه کردن به کاراکتر بعدی بدون مصرف آن
        /// </summary>
        private char PeekNext() => (_current + 1 >= _source.Length) ? '\0' : _source[_current + 1];

        /// <summary>
        /// تشخیص اینکه آیا کاراکتر مجاز به شروع شناسه هست (حروف یا زیرخط)
        /// </summary>
        private bool IsAlpha(char c) => char.IsLetter(c) || c == '_';
    }
}
