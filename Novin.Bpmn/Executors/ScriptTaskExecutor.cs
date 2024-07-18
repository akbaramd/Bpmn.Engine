using Novin.Bpmn.Test.Core;
using Novin.Bpmn.Test.Executors.Abstracts;
using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test.Executors
{
    public class ScriptTaskExecutor : ITaskExecutor
    {
        private readonly ScriptHandler _scriptHandler = new();

        public async Task<List<string>?> ExecuteAsync(BpmnFlowElement element, ProcessEngine engine)
        {
            if (element is BpmnScriptTask scriptTask)
            {
                var scriptContent = scriptTask.script.InnerText;
                try
                {
                    var globals = new ScriptGlobals { State = engine.State };
                    await _scriptHandler.ExecuteScriptAsync(scriptContent, globals);
                    return null;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error executing script: {ex.Message}");
                }
            }

            return null;
        }
    }
}