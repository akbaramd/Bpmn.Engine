using System.Runtime.Serialization;

namespace Novin.Bpmn.EventSourcing.Feel
{
    /// <summary>
    /// بازدیدکننده (Visitor) برای ارزیابی AST FEEL و تولید مقدار نتیجه.
    /// </summary>
    public sealed class EvalVisitor : IExpressionVisitor<object?>
    {
        private readonly IDictionary<string, object?> _vars;

        public EvalVisitor(IDictionary<string, object?> vars) => _vars = vars ?? throw new ArgumentNullException(nameof(vars));

        public object? VisitBinary(BinaryExpr expr)
        {
            var left = expr.Left.Accept(this);
            var right = expr.Right.Accept(this);

            // جلوگیری از مقدار null غیرمنتظره
            if (left is null || right is null)
                throw new FeelRuntimeException($"Binary operator '{expr.Op.Type}' received null operand.");

            switch (expr.Op.Type)
            {
                case TokenType.Plus: return Add(left, right);
                case TokenType.Minus: return Subtract(left, right);
                case TokenType.Star: return Multiply(left, right);
                case TokenType.Slash: return Divide(left, right);
                case TokenType.Percent: return Modulo(left, right);
                case TokenType.Equals: return Equals(left, right);
                case TokenType.NotEquals: return !Equals(left, right);
                case TokenType.Greater: return Compare(left, right) > 0;
                case TokenType.GreaterEq: return Compare(left, right) >= 0;
                case TokenType.Less: return Compare(left, right) < 0;
                case TokenType.LessEq: return Compare(left, right) <= 0;
                case TokenType.And: return ToBool(left) && ToBool(right);
                case TokenType.Or: return ToBool(left) || ToBool(right);
                case TokenType.In: return CheckIn(left, right);
                case TokenType.Power:
                    return Math.Pow(Convert.ToDouble(left), Convert.ToDouble(right));
                default: throw new FeelRuntimeException($"Unsupported binary operator {expr.Op.Type}");
            }
        }

        public object? VisitUnary(UnaryExpr expr)
        {
            var right = expr.Right.Accept(this);
            if (right is null)
                throw new FeelRuntimeException($"Unary operator '{expr.Op.Type}' received null operand.");

            switch (expr.Op.Type)
            {
                case TokenType.Not: return !ToBool(right);
                case TokenType.Minus: return Negate(right);
                default: throw new FeelRuntimeException($"Unsupported unary operator {expr.Op.Type}");
            }
        }

        public object? VisitLiteral(LiteralExpr expr) => expr.Value;

        public object? VisitIdentifier(IdentifierExpr expr)
        {
            if (!_vars.TryGetValue(expr.Name, out var v))
                throw new FeelRuntimeException($"Variable '{expr.Name}' not provided");
            return v;
        }

        public object? VisitRange(RangeExpr expr)
        {
            var start = expr.Start.Accept(this);
            var end = expr.End.Accept(this);
            return new Range(start, end, expr.InclusiveStart, expr.InclusiveEnd);
        }

        public object? VisitList(ListExpr expr)
        {
            return expr.Elements.Select(e => e.Accept(this)).ToList();
        }

        public object? VisitContext(ContextExpr expr)
        {
            return expr.Entries.ToDictionary(kv => kv.Key, kv => kv.Value.Accept(this));
        }

        public object? VisitConditional(ConditionalExpr expr)
        {
            return ToBool(expr.Condition.Accept(this)) ? expr.ThenBranch.Accept(this) : expr.ElseBranch?.Accept(this);
        }

        public object? VisitFuncCall(FuncCallExpr expr)
        {
            var args = expr.Arguments.Select(a => a.Accept(this)).ToArray();
            if (BuiltIns.TryInvoke(expr.Identifier, args, out var result))
                return result;
            throw new FeelRuntimeException($"Unknown function '{expr.Identifier}'");
        }

        // =======================
        // متدهای کمکی تبدیل و عملیات
        // =======================

        private static bool ToBool(object? value)
        {
            if (value is bool b) return b;
            throw new FeelRuntimeException($"Expected boolean value but got '{value?.GetType().Name ?? "null"}'.");
        }

        private static object Add(object left, object right)
        {
            if (left is string || right is string)
                return $"{left}{right}";

            if (TryConvertToDecimal(left, out var lDec) && TryConvertToDecimal(right, out var rDec))
                return lDec + rDec;

            throw new FeelRuntimeException($"Cannot add types {left.GetType()} and {right.GetType()}");
        }

        private static object Subtract(object left, object right)
        {
            if (TryConvertToDecimal(left, out var lDec) && TryConvertToDecimal(right, out var rDec))
                return lDec - rDec;

            throw new FeelRuntimeException($"Cannot subtract types {left.GetType()} and {right.GetType()}");
        }

        private static object Multiply(object left, object right)
        {
            if (TryConvertToDecimal(left, out var lDec) && TryConvertToDecimal(right, out var rDec))
                return lDec * rDec;

            throw new FeelRuntimeException($"Cannot multiply types {left.GetType()} and {right.GetType()}");
        }

        private static object Divide(object left, object right)
        {
            if (TryConvertToDecimal(left, out var lDec) && TryConvertToDecimal(right, out var rDec))
            {
                if (rDec == 0) throw new FeelRuntimeException("Division by zero");
                return lDec / rDec;
            }
            throw new FeelRuntimeException($"Cannot divide types {left.GetType()} and {right.GetType()}");
        }

        private static object Modulo(object left, object right)
        {
            if (TryConvertToDecimal(left, out var lDec) && TryConvertToDecimal(right, out var rDec))
            {
                if (rDec == 0) throw new FeelRuntimeException("Modulo by zero");
                return lDec % rDec;
            }
            throw new FeelRuntimeException($"Cannot modulo types {left.GetType()} and {right.GetType()}");
        }

        private static object Negate(object value)
        {
            if (TryConvertToDecimal(value, out var dec))
                return -dec;

            throw new FeelRuntimeException($"Cannot negate type {value.GetType()}");
        }

        private static int Compare(object left, object right)
        {
            if (TryConvertToDecimal(left, out var lDec) && TryConvertToDecimal(right, out var rDec))
                return lDec.CompareTo(rDec);

            if (left is string ls && right is string rs)
                return string.Compare(ls, rs, StringComparison.Ordinal);

            throw new FeelRuntimeException($"Cannot compare types {left.GetType()} and {right.GetType()}");
        }

        private static bool CheckIn(object left, object right)
        {
            if (right is Range r)
                return r.Contains(left);

            if (right is IEnumerable<object?> list)
                return list.Contains(left);

            if (right is decimal d && TryConvertToDecimal(left, out var lDec))
                return lDec == d;

            if (right is int i && int.TryParse(left.ToString(), out var lInt))
                return lInt == i;

            throw new FeelRuntimeException($"Unsupported 'in' target: {right?.GetType().Name ?? "null"}");
        }

        private static bool TryConvertToDecimal(object obj, out decimal value)
        {
            switch (obj)
            {
                case decimal d:
                    value = d;
                    return true;
                case int i:
                    value = i;
                    return true;
                case long l:
                    value = l;
                    return true;
                case short s:
                    value = s;
                    return true;
                case float f:
                    value = (decimal)f;
                    return true;
                case double db:
                    value = (decimal)db;
                    return true;
                case string str when decimal.TryParse(str, out var parsed):
                    value = parsed;
                    return true;
                default:
                    value = 0;
                    return false;
            }
        }
    }

    // ================================
    // Exception Classes for FEEL errors
    // ================================

    /// <summary>
    /// پایه تمام خطاهای FEEL
    /// </summary>
    [Serializable]
    public class FeelException : Exception
    {
        public FeelException() { }
        public FeelException(string message) : base(message) { }
        public FeelException(string message, Exception inner) : base(message, inner) { }
        protected FeelException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }

    /// <summary>
    /// خطاهای زمان اجرا مانند نوع ناسازگار، متغیر ناشناخته، تقسیم بر صفر و غیره
    /// </summary>
    [Serializable]
    public class FeelRuntimeException : FeelException
    {
        public FeelRuntimeException() { }
        public FeelRuntimeException(string message) : base(message) { }
        public FeelRuntimeException(string message, Exception inner) : base(message, inner) { }
        protected FeelRuntimeException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }

    /// <summary>
    /// خطاهای مرحله توکن‌سازی (لکسینگ)
    /// </summary>
    [Serializable]
    public class FeelLexException : FeelException
    {
        public FeelLexException() { }
        public FeelLexException(string message) : base(message) { }
        public FeelLexException(string message, Exception inner) : base(message, inner) { }
        protected FeelLexException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }

    /// <summary>
    /// خطاهای نحوی/پارسیگ
    /// </summary>
    [Serializable]
    public class FeelParseException : FeelException
    {
        public FeelParseException() { }
        public FeelParseException(string message) : base(message) { }
        public FeelParseException(string message, Exception inner) : base(message, inner) { }
        protected FeelParseException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
