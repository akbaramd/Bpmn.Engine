using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Core.Models;

public class ScriptExecuter
{
    private readonly ScriptOptions _scriptOptions;

    public ScriptExecuter()
    {
        _scriptOptions = ScriptOptions.Default
            .WithImports("System", "System.Linq", "System.Collections.Generic", "Novin.Bpmn.EventSourcing.Core.Models")
            .WithReferences(
                typeof(object).Assembly,
                typeof(Dictionary<,>).Assembly,
                typeof(ElementExecution).Assembly
            );
    }

    /// <summary>
    /// Executes a C# script (non-returning) in the context of a BPMN ElementExecution.
    /// </summary>
    public async Task Execute(string script, ElementExecution execution)
    {
        if (string.IsNullOrWhiteSpace(script))
            return;

        try
        {
            var globals = new ScriptGlobals(execution);
            await CSharpScript.RunAsync(script, _scriptOptions, globals);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Evaluates a C# boolean expression against the ElementExecution context.
    /// </summary>
    public async Task<bool> Evaluate(string expression, ElementExecution execution)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return true;

        try
        {
            var globals = new ScriptGlobals(execution);
            var result = await CSharpScript.EvaluateAsync<bool>(expression, _scriptOptions, globals);
            return result;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    /// <summary>
    /// Globally accessible context passed to the scripting engine.
    /// </summary>
    public class ScriptGlobals
    {
        public ElementExecution execution { get; }

        public ScriptGlobals(ElementExecution execution)
        {
            this.execution = execution ?? throw new ArgumentNullException(nameof(execution));
        }
    }
}
