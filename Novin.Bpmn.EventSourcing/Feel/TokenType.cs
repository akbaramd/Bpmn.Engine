namespace Novin.Bpmn.EventSourcing.Feel;

public enum TokenType
{
    // Single-char
    Plus, Minus, Star, Slash, Percent,
    LParen, RParen, LBracket, RBracket, LBrace, RBrace, Comma, Dot,

    // Two-char or mixed
    Equals, NotEquals, Greater, GreaterEq, Less, LessEq,
    And, Or, Not, In, Range, // ..  (inclusive range)

    // Literals / identifiers
    Identifier, Number, String, Boolean, Null,

    // Keywords
    If, Then, Else, Between, // etc.

    EoF,
    Colon,
    Power, // نمایانگر اپراتور توان (^)
}

/// <summary>
/// رکورد توکن: نوع، مقدار (Lexeme) و محل دقیق آن در ورودی (خط و ستون) را نگهداری می‌کند.
/// </summary>
public record Token(TokenType Type, string Lexeme, int Line, int Column);