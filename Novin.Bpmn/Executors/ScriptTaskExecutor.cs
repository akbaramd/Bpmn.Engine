using Novin.Bpmn.Test.Core;
using Novin.Bpmn.Test.Executors.Abstracts;
using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test.Executors;

public class ScriptTaskExecutor : ITaskExecutor
{
    private readonly ScriptHandler _scriptHandler = new();

    public async Task ExecuteAsync(BpmnFlowElement element, BpmnInstance? context)
    {
        if (element is BpmnScriptTask scriptTask)
        {
            var scriptContent = scriptTask.script.InnerText;
            Console.WriteLine("Executing script:");
            Console.WriteLine(scriptContent);

            try
            {
                var globals = new ScriptGlobals { Instance = context };
                await _scriptHandler.ExecuteScriptAsync(scriptContent, globals);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing script: {ex.Message}");
            }
        }
    }

}