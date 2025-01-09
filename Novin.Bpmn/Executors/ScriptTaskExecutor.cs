using Novin.Bpmn.Core;
using Novin.Bpmn.Models;
using System;
using Novin.Bpmn.Abstractions;

namespace Novin.Bpmn.Executors
{
    public class ScriptTaskExecutor : IScriptTaskExecutor
    {
        private readonly ScriptHandler _scriptHandler = new();

        public async Task ExecuteAsync(BpmnProcessNode processNode, BpmnProcessExecutor processExecutor)
        {
            Console.WriteLine($"Starting script execution for node {processNode.ElementId}");

            var definitionsHandler = new BpmnDefinitionsHandler(processExecutor.Instance.Definition);

            // Retrieve the process element by ID
            var element = definitionsHandler.GetElementById(processNode.ElementId);
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

            try
            {
                // Prepare globals for script execution
                var globals = CreateScriptGlobals(processExecutor);

                // Execute the script asynchronously
                await _scriptHandler.ExecuteScriptAsync(scriptContent, globals);

                Console.WriteLine($"Script executed successfully for node {processNode.ElementId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing script for node {processNode.ElementId}: {ex.Message}");

                // Log the exception in the process node for tracking
                processNode.LogException($"Script execution error: {ex.Message}");
            }
        }

        private ScriptGlobals CreateScriptGlobals(BpmnProcessExecutor processExecutor)
        {
            try
            {
                // Create and configure script globals
                return new ScriptGlobals { State = processExecutor.Instance };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating script globals: {ex.Message}");
                throw; // Re-throw to propagate the error
            }
        }
    }
}
