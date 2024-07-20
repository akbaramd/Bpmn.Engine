using Novin.Bpmn.Core;
using Novin.Bpmn.Executors.Abstracts;
using Novin.Bpmn.Models;

namespace Novin.Bpmn.Executors
{
    public class ScriptTaskExecutor : ITaskExecutor
    {
        private readonly ScriptHandler _scriptHandler = new();

        public async Task ExecuteAsync(BpmnNode node, BpmnEngine engine)
        {
            var element = engine.DefinitionsHandler.GetElementById(node.Id);
            if (element is BpmnScriptTask scriptTask)
            {
                var scriptContent = scriptTask.script.InnerText;
                try
                {
                    var globals = new ScriptGlobals { State = engine.State };
                    await _scriptHandler.ExecuteScriptAsync(scriptContent, globals);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error executing script: {ex.Message}");
                }
            }
        }
    }
}