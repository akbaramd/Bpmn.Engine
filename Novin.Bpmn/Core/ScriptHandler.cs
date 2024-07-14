using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CSharp.RuntimeBinder;

namespace Novin.Bpmn.Test.Core;

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
                    MetadataReference.CreateFromFile(typeof(Binder).Assembly.Location)
                }))
            .WithImports(
                "System",
                "System.Dynamic",
                "System.Xml.Serialization",
                "System.Collections.Generic",
                "System.Linq",
                "System.Text",
                "System.Threading.Tasks");
    }

    public async Task ExecuteScriptAsync(string scriptContent, object globals)
    {
        await CSharpScript.RunAsync(scriptContent, _scriptOptions, globals);
    }

    public async Task<bool> EvaluateConditionAsync(string condition, object globals)
    {
        return await CSharpScript.EvaluateAsync<bool>(condition, _scriptOptions, globals);
    }
}