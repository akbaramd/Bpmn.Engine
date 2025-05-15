namespace Novin.Bpmn.EventSourcing.Feel
{
    /// <summary>
    /// موتور ارزیابی عبارات FEEL با قابلیت تبدیل به نوع strongly-typed
    /// </summary>
    public static class FeelEngine
    {
        /// <summary>
        /// ارزیابی عبارت FEEL و بازگرداندن نتیجه strongly-typed از نوع T
        /// </summary>
        /// <typeparam name="T">نوع مورد انتظار خروجی</typeparam>
        /// <param name="feelExpression">عبارت FEEL (می‌تواند با '=' شروع شود)</param>
        /// <param name="variables">متغیرهای ورودی به صورت دیکشنری</param>
        /// <returns>نتیجه تبدیل شده به نوع T</returns>
        /// <exception cref="FeelException">خطاهای تحلیل یا اجرا در FEEL</exception>
        public static T Evaluate<T>(string feelExpression, IDictionary<string, object?> variables)
        {
            if (string.IsNullOrWhiteSpace(feelExpression))
                throw new ArgumentException("FEEL expression cannot be null or empty.", nameof(feelExpression));

            // حذف '=' ابتدا (اگر وجود دارد) و حذف فضای سفید
            if (feelExpression.StartsWith("="))
                feelExpression = feelExpression[1..].TrimStart();

            try
            {
                // ساخت توکن‌ها و پارس کردن به AST
                var tokens = new Lexer(feelExpression).ScanTokens();
                var ast = new Parser(tokens).Parse();

                // ارزیابی AST با مقادیر متغیرها
                var result = ast.Accept(new EvalVisitor(variables));

                // کنترل خروجی null و عدم تطابق با نوع T
                if (result == null && Nullable.GetUnderlyingType(typeof(T)) == null && typeof(T).IsValueType)
                    throw new InvalidCastException($"FEEL expression result is null but requested type {typeof(T)} is non-nullable.");

                // تبدیل ایمن به نوع مقصد
                return (T)Convert.ChangeType(result!, typeof(T));
            }
            catch (FeelException)
            {
                // عبور خطاهای تخصصی FEEL بدون تغییر
                throw;
            }
            catch (Exception ex)
            {
                // تبدیل خطاهای عمومی به خطای FEEL
                throw new FeelException("Error evaluating FEEL expression.", ex);
            }
        }

        /// <summary>
        /// ارزیابی FEEL و ذخیره نتیجه در دیکشنری خروجی با نام متغیر مشخص شده
        /// </summary>
        /// <param name="feelExpression">عبارت FEEL</param>
        /// <param name="variables">متغیرهای ورودی و خروجی به صورت دیکشنری</param>
        /// <param name="resultVariableName">نام متغیر خروجی که نتیجه در آن ذخیره شود</param>
        public static void EvaluateAndStore(string feelExpression, IDictionary<string, object?> variables, string resultVariableName)
        {
            if (string.IsNullOrWhiteSpace(resultVariableName))
                throw new ArgumentException("Result variable name cannot be null or empty.", nameof(resultVariableName));

            var result = Evaluate<object>(feelExpression, variables);

            variables[resultVariableName] = result;
        }
    }
}
