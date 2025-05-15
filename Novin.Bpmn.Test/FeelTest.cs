using Novin.Bpmn.EventSourcing.Feel;

namespace Novin.Bpmn.Test;

public class FeelEngineAdvancedTests
{
    [Fact]
    public void Evaluate_ComplexArithmeticExpression_ReturnsCorrectDecimal()
    {
        var vars = new Dictionary<string, object?> { ["x"] = 10, ["y"] = 5, ["z"] = 2 };
        var expr = "= (x + y) * z - 4 / 2";
        var result = FeelEngine.Evaluate<decimal>(expr, vars);
        Assert.Equal(28, result);
    }

    [Fact]
    public void Evaluate_LogicalExpressionsWithOrAnd_ReturnsExpectedBoolean()
    {
        var vars = new Dictionary<string, object?> { ["a"] = true, ["b"] = false, ["c"] = true };
        var expr = "= a and (b or c)";
        var result = FeelEngine.Evaluate<bool>(expr, vars);
        Assert.True(result);
    }

    [Fact]
    public void Evaluate_IfThenElseWithNestedConditions_ReturnsCorrectString()
    {
        var vars = new Dictionary<string, object?> { ["score"] = 85 };
        var expr = "= if score >= 90 then \"A\" else if score >= 80 then \"B\" else \"C\"";
        var result = FeelEngine.Evaluate<string>(expr, vars);
        Assert.Equal("B", result);
    }

    [Fact]
    public void Evaluate_FunctionCallWithArguments_SumAndCount()
    {
        var vars = new Dictionary<string, object?> {
            ["values"] = new List<decimal> { 10, 20, 30 },
            ["emptyList"] = new List<decimal>()
        };
        var sumResult = FeelEngine.Evaluate<decimal>("= sum(values)", vars);
        var countResult = FeelEngine.Evaluate<decimal>("= count(values)", vars);
        var emptyCount = FeelEngine.Evaluate<decimal>("= count(emptyList)", vars);

        Assert.Equal(60, sumResult);
        Assert.Equal(3, countResult);
        Assert.Equal(0, emptyCount);
    }

    [Fact]
    public void Evaluate_RangeAndBetweenExpressions_ReturnsBoolean()
    {
        var vars = new Dictionary<string, object?> { ["temp"] = 25 };
        var exprInRange = "= temp in 20..30";
        var exprBetween = "= temp between 20 and 30";

        Assert.True(FeelEngine.Evaluate<bool>(exprInRange, vars));
        Assert.True(FeelEngine.Evaluate<bool>(exprBetween, vars));
    }
    [Fact]
    public void Evaluate_VeryComplexMathExpression_PassesStressTest()
    {
        var vars = new Dictionary<string, object?>
        {
            ["x"] = 3m,
            ["y"] = 4m,
            ["z"] = 2m
        };

        var expr = "= ((x ^ 2 + y ^ 2) ^ (1 / 2) + max([x, y, z]) * sum([x, y, z])) / (if z = 0 then 1 else z) - count([x, y, z]) * 3 + if (x > y and y < z) or (z in 1..10) then 100 else 50";



        var result = FeelEngine.Evaluate<decimal>(expr, vars);

        // محاسبه دستی:
        // ((3^2 + 4^2)^(1/2) + max(3,4,2) * sum(3,4,2)) / 2 - 3*3 + 100
        // ( (9+16)^(0.5) + 4*9 ) / 2 - 9 + 100
        // (5 + 36) / 2 - 9 + 100
        // 41/2 - 9 + 100
        // 20.5 - 9 + 100 = 111.5

        Assert.Equal(111.5m, result);
    }

    [Fact]
    public void Evaluate_ListLiteralAndContextLiteral_ReturnsExpectedObjects()
    {
        var vars = new Dictionary<string, object?>();
        var listExpr = "= [1, 2, 3, 4]";
        var contextExpr = "= { name: \"Alice\", age: 30, scores: [90, 85, 88] }";

        var listResult = FeelEngine.Evaluate<List<object?>>(listExpr, vars);
        var contextResult = FeelEngine.Evaluate<Dictionary<string, object?>>(contextExpr, vars);

        Assert.Equal(new List<object?> { 1m, 2m, 3m, 4m }, listResult);
        Assert.Equal("Alice", contextResult["name"]);
        Assert.Equal(30m, contextResult["age"]);
        Assert.Equal(new List<object?> { 90m, 85m, 88m }, contextResult["scores"]);
    }

    [Fact]
    public void Evaluate_StringConcatenationAndArithmeticMix_ReturnsCorrectString()
    {
        var vars = new Dictionary<string, object?> { ["firstName"] = "Jane", ["lastName"] = "Smith", ["age"] = 28 };
        var expr = "= firstName + \" \" + lastName + \" is \" + age + \" years old\"";
        var result = FeelEngine.Evaluate<string>(expr, vars);
        Assert.Equal("Jane Smith is 28 years old", result);
    }

    [Fact]
    public void Evaluate_UnaryOperators_NotAndMinus_WorkCorrectly()
    {
        var vars = new Dictionary<string, object?> { ["flag"] = false, ["value"] = 5 };
        var notExpr = "= not flag";
        var minusExpr = "= -value";

        Assert.True(FeelEngine.Evaluate<bool>(notExpr, vars));
        Assert.Equal(-5m, FeelEngine.Evaluate<decimal>(minusExpr, vars));
    }

    [Fact]
    public void Evaluate_ThrowsFeelParseException_OnInvalidSyntax()
    {
        var vars = new Dictionary<string, object?>();
        var invalidExpr = "= if true then";

        Assert.Throws<FeelParseException>(() => FeelEngine.Evaluate<object>(invalidExpr, vars));
    }

    [Fact]
    public void Evaluate_ThrowsFeelRuntimeException_OnUnknownVariable()
    {
        var vars = new Dictionary<string, object?>();
        var expr = "= unknownVar + 1";

        var ex = Assert.Throws<FeelRuntimeException>(() => FeelEngine.Evaluate<object>(expr, vars));
        Assert.Contains("unknownVar", ex.Message);
    }

    [Fact]
    public void Evaluate_HandlesNullsCorrectly()
    {
        var vars = new Dictionary<string, object?> { ["value"] = null };
        var exprNullCheck = "= value = null";
        var exprIfNull = "= if value = null then \"no value\" else \"has value\"";

        Assert.True(FeelEngine.Evaluate<bool>(exprNullCheck, vars));
        Assert.Equal("no value", FeelEngine.Evaluate<string>(exprIfNull, vars));
    }

    [Fact]
    public void Evaluate_HandlesNestedFunctionCalls()
    {
        var vars = new Dictionary<string, object?> {
            ["numbers"] = new List<decimal> { 1, 2, 3, 4 }
        };

        var expr = "= sum([count(numbers), 10, 20])";  // sum of count(numbers) + 10 + 20 = 4 + 10 + 20 = 34
        var result = FeelEngine.Evaluate<decimal>(expr, vars);

        Assert.Equal(34, result);
    }
}
