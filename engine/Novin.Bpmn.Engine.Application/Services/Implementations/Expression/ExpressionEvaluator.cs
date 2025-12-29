using System.Linq.Expressions;

namespace Novin.Bpmn.Engine.Application.Services;

public class ExpressionEvaluator
{
    // ارزیابی شرط‌های C# با استفاده از Expression API
    // Variables are now stored as strings, so we need to parse them when evaluating
    public bool Evaluate(string expression, Dictionary<string, string> variables)
    {
        try
        {
            // Convert string dictionary to object dictionary for expression evaluation
            // Parse string values to appropriate types (int, double, bool, string)
            var objectVariables = new Dictionary<string, object>();
            foreach (var kvp in variables)
            {
                objectVariables[kvp.Key] = ParseValue(kvp.Value);
            }

            // ساخت پارامتر برای expression با استفاده از متغیرها
            var parameter = Expression.Parameter(typeof(Dictionary<string, object>), "variables");

            // بررسی نحوه تبدیل رشته‌ها به Expression
            var expressionBody = ParseExpression(expression, parameter);

            // ساخت lambda از expression
            var lambda = Expression.Lambda<Func<Dictionary<string, object>, bool>>(expressionBody, parameter);
            var compiledLambda = lambda.Compile();

            // اجرای lambda با متغیرهای ورودی
            var result = compiledLambda(objectVariables);
            return result;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to evaluate expression: {expression}", ex);
        }
    }

    /// <summary>
    /// Parses a string value to an appropriate object type (int, double, bool, or string)
    /// </summary>
    private static object ParseValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        // Try parsing as boolean
        if (bool.TryParse(value, out var boolValue))
            return boolValue;

        // Try parsing as integer
        if (int.TryParse(value, out var intValue))
            return intValue;

        // Try parsing as double
        if (double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var doubleValue))
            return doubleValue;

        // Return as string
        return value;
    }

    // این متد رشته‌ی شرط را به Expression تبدیل می‌کند
    private Expression ParseExpression(string expression, ParameterExpression parameter)
    {
        // اینجا از Regex یا پارسرهای مختلف می‌توانید برای تبدیل رشته‌ها به عبارات استفاده کنید
        // برای سادگی، فرض می‌کنیم که فقط به `amount > 100` نیاز داریم.
        // در واقع شما باید پارس‌کننده مخصوص برای شرایط FEEL و مشابه بنویسید.

        // فرضاً در اینجا یک مثال ساده داریم:
        if (expression.Contains(">"))
        {
            var parts = expression.Split('>');
            var left = Expression.Property(parameter, parts[0].Trim());
            var right = Expression.Constant(int.Parse(parts[1].Trim()));
            return Expression.GreaterThan(left, right);
        }

        if (expression.Contains("="))
        {
            var parts = expression.Split('=');
            var left = Expression.Property(parameter, parts[0].Trim());
            var right = Expression.Constant(parts[1].Trim().Replace("\"", ""));
            return Expression.Equal(left, right);
        }

        throw new InvalidOperationException("Unsupported expression format.");
    }
}