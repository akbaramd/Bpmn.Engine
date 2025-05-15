namespace Novin.Bpmn.EventSourcing.Feel;

public interface IExpression
{
    T Accept<T>(IExpressionVisitor<T> visitor);
}

public interface IExpressionVisitor<out T>
{
    T VisitBinary(BinaryExpr expr);
    T VisitUnary(UnaryExpr expr);
    T VisitLiteral(LiteralExpr expr);
    T VisitIdentifier(IdentifierExpr expr);
    T VisitRange(RangeExpr expr);
    T VisitList(ListExpr expr);
    T VisitContext(ContextExpr expr);
    T VisitConditional(ConditionalExpr expr);
    T VisitFuncCall(FuncCallExpr expr);
}



public sealed record ListExpr(IReadOnlyList<IExpression> Elements) : IExpression
{
    public T Accept<T>(IExpressionVisitor<T> v) => v.VisitList(this);
}

public sealed record ContextExpr(IReadOnlyDictionary<string, IExpression> Entries) : IExpression
{
    public T Accept<T>(IExpressionVisitor<T> v) => v.VisitContext(this);
}

public sealed record ConditionalExpr(
    IExpression Condition,
    IExpression ThenBranch,
    IExpression? ElseBranch) : IExpression
{
    public T Accept<T>(IExpressionVisitor<T> v) => v.VisitConditional(this);
}

public sealed record FuncCallExpr(
    string Identifier,
    IReadOnlyList<IExpression> Arguments) : IExpression
{
    public T Accept<T>(IExpressionVisitor<T> v) => v.VisitFuncCall(this);
}
public sealed record BinaryExpr(IExpression Left, Token Op, IExpression Right) : IExpression
{
    public T Accept<T>(IExpressionVisitor<T> visitor) => visitor.VisitBinary(this);
}

public sealed record UnaryExpr(Token Op, IExpression Right) : IExpression
{
    public T Accept<T>(IExpressionVisitor<T> v) => v.VisitUnary(this);
}

public sealed record LiteralExpr(object? Value) : IExpression
{
    public T Accept<T>(IExpressionVisitor<T> v) => v.VisitLiteral(this);
}

public sealed record IdentifierExpr(string Name) : IExpression
{
    public T Accept<T>(IExpressionVisitor<T> v) => v.VisitIdentifier(this);
}

public sealed record RangeExpr(IExpression Start, bool InclusiveStart,
                               IExpression End,   bool InclusiveEnd) : IExpression
{
    public T Accept<T>(IExpressionVisitor<T> v) => v.VisitRange(this);
}

// … ListExpr, ContextExpr, ConditionalExpr (if then else), FuncCallExpr
