using Novin.Bpmn.Core;
using Novin.Bpmn.Executors.Abstracts;
using Novin.Bpmn.Models;

namespace Novin.Bpmn.Executors
{
    public class ScriptTaskExecutor : ITaskExecutor
    {
        private readonly ScriptHandler _scriptHandler = new();

        public async Task ExecuteAsync(BpmnProcessNode processNode, BpmnProcessEngine processEngine)
        {
            var element = processEngine.DefinitionsHandler.GetElementById(processNode.ElementId);
            if (element is BpmnScriptTask scriptTask)
            {
                var scriptContent = scriptTask.script.InnerText;
                try
                {
                    var globals = new ScriptGlobals { State = processEngine.ProcessState };
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