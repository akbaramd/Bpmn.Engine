namespace Novin.Bpmn.Engine.Application.Services;

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