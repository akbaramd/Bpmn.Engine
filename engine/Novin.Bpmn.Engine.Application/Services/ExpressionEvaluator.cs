using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

public class ExpressionEvaluator
{
    // ارزیابی شرط‌های C# با استفاده از Expression API
    public bool Evaluate(string expression, Dictionary<string, object> variables)
    {
        try
        {
            // ساخت پارامتر برای expression با استفاده از متغیرها
            var parameter = Expression.Parameter(typeof(Dictionary<string, object>), "variables");

            // بررسی نحوه تبدیل رشته‌ها به Expression
            var expressionBody = ParseExpression(expression, parameter);

            // ساخت lambda از expression
            var lambda = Expression.Lambda<Func<Dictionary<string, object>, bool>>(expressionBody, parameter);
            var compiledLambda = lambda.Compile();

            // اجرای lambda با متغیرهای ورودی
            var result = compiledLambda(variables);
            return result;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to evaluate expression: {expression}", ex);
        }
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
