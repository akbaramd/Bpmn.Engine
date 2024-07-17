using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CSharp.RuntimeBinder;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Binder = Microsoft.CSharp.RuntimeBinder.Binder;

namespace Novin.Bpmn.Test.Core
{
    public class ScriptHandler
    {
        private readonly ScriptOptions _scriptOptions;

        public ScriptHandler()
        {
            _scriptOptions = ScriptOptions.Default
                .WithReferences(AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                    .Select(a => MetadataReference.CreateFromFile(a.Location))
                    .Concat(new[]
                    {
                        MetadataReference.CreateFromFile(typeof(Console).GetTypeInfo().Assembly.Location),
                        MetadataReference.CreateFromFile(typeof(Binder).GetTypeInfo().Assembly.Location),
                        MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location),
                        MetadataReference.CreateFromFile(typeof(Enumerable).GetTypeInfo().Assembly.Location),
                        MetadataReference.CreateFromFile(typeof(System.Dynamic.ExpandoObject).Assembly.Location)
                    }))
                .WithImports(
                    "System",
                    "System.Dynamic",
                    "Microsoft.CSharp",
                    "System.Collections.Generic",
                    "System.Linq",
                    "System.Text",
                    "System.Threading.Tasks");
        }

        public async Task ExecuteScriptAsync(string scriptContent, object globals)
        {
            try
            {
                await CSharpScript.RunAsync(scriptContent, _scriptOptions, globals);
            }
            catch (CompilationErrorException e)
            {
                Console.WriteLine($"Error executing script: {string.Join(Environment.NewLine, e.Diagnostics)}");
                throw;
            }
        }

        public async Task<bool> EvaluateConditionAsync(string condition, object globals)
        {
            try
            {
                return await CSharpScript.EvaluateAsync<bool>(condition, _scriptOptions, globals);
            }
            catch (CompilationErrorException e)
            {
                Console.WriteLine($"Error evaluating condition: {string.Join(Environment.NewLine, e.Diagnostics)}");
                throw;
            }
        }
    }
}
