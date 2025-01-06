using Novin.Bpmn.Core;
using Novin.Bpmn.Executors.Abstracts;
using Novin.Bpmn.Models;

namespace Novin.Bpmn.Executors
{
    public class ScriptTaskExecutor : IScriptTaskExecutor
    {
        private readonly ScriptHandler _scriptHandler = new();

        public async Task ExecuteAsync(BpmnProcessNode processNode, BpmnProcessEngine processEngine)
        {
            var definitionsHandler = new BpmnDefinitionsHandler(processEngine.Instance.Definition);
            var element = definitionsHandler.GetElementById(processNode.ElementId);
            if (element is BpmnScriptTask scriptTask)
            {
                var scriptContent = scriptTask.script.InnerText;
                try
                {
                    var globals = new ScriptGlobals { State = processEngine.Instance };
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