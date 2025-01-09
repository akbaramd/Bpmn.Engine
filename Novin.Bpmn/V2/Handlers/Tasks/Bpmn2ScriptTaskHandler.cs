using Novin.Bpmn.Core;
using Novin.Bpmn.Models;
using Novin.Bpmn.V2.Abstractions;

namespace Novin.Bpmn.V2.Handlers.Tasks
{
    public class Bpmn2ScriptTaskHandler : IBpmnV2ScriptTaskHandler
    {
        private readonly ScriptHandler _scriptHandler = new();

        public async Task HandleAsync(BpmnProcessNode processNode, BpmnProcessInstance instance,CancellationToken cancellationToken)
        {
            Console.WriteLine($"Starting script execution for node {processNode.ElementId}");


            // Retrieve the process element by ID
            var element = instance.DefinitionsHandler.GetElementById(processNode.ElementId);
            if (element is not BpmnScriptTask scriptTask)
            {
                Console.WriteLine($"Element {processNode.ElementId} is not a script task. Skipping execution.");
                return;
            }

            // Retrieve the script content
            var scriptContent = scriptTask.script?.InnerText;
            if (string.IsNullOrWhiteSpace(scriptContent))
            {
                Console.WriteLine($"Script content for node {processNode.ElementId} is null or empty. Skipping execution.");
                return;
            }

                // Prepare globals for script execution
                var globals = CreateScriptGlobals(instance);

                // Execute the script asynchronously
                await _scriptHandler.ExecuteScriptAsync(scriptContent, globals);

                Console.WriteLine($"Script executed successfully for node {processNode.ElementId}");
        }

        private ScriptGlobals CreateScriptGlobals(BpmnProcessInstance instance)
        {
            try
            {
                // Create and configure script globals
                return new ScriptGlobals { State = instance };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating script globals: {ex.Message}");
                throw; // Re-throw to propagate the error
            }
        }
    }
}
