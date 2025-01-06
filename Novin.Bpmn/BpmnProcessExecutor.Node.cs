using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Models;
using Novin.Bpmn.Handlers;

namespace Novin.Bpmn
{
    public partial class BpmnProcessExecutor
    {
        private async Task ProcessNodeWithRetries(BpmnProcessNode processNode, int maxRetries = 3)
        {
            int attempts = 0;
            while (attempts < maxRetries)
            {
                try
                {
                    await ProcessNode(processNode);
                    return; // Success, exit retry loop
                }
                catch (Exception ex)
                {
                    attempts++;
                    if (attempts >= maxRetries)
                        throw; // Rethrow after exhausting retries
                }
            }
        }

        private async Task ProcessNode(BpmnProcessNode processNode)
        {
            if (Instance.IsStopped) return;

            await WaitIfPaused();

            try
            {
                processNode.Details = $"Executed at {DateTime.Now}";

                if (processNode.IsExecutable)
                    await ExecuteTask(processNode);

                if (processNode.CanBeContinue)
                    await FindNextNodes(processNode);

                StoreProcessState();
            }
            catch (Exception ex)
            {
                HandleNodeException(processNode, ex);
                throw;
            }
        }

        private async Task ExecuteTask(BpmnProcessNode processNode)
        {
            await ManageMainActivity(processNode);
        }

        private async Task ManageMainActivity(BpmnProcessNode processNode)
        {
            try
            {
                var element = DefinitionsHandler.GetElementById(processNode.ElementId);

                switch (element)
                {
                    case BpmnUserTask userTask:
                        Console.WriteLine($"Starting user task {userTask.id}");
                        await ExecuteUserTask(processNode);
                        break;

                    case BpmnScriptTask scriptTask:
                        Console.WriteLine($"Starting script task {scriptTask.id}");
                        await ExecuteScriptTask(processNode);
                        break;

                    case BpmnServiceTask serviceTask:
                        Console.WriteLine($"Starting service task {serviceTask.id}");
                        await ServiceTaskExecutor.ExecuteAsync(processNode, this);
                        break;

                    default:
                        Console.WriteLine($"Unhandled task type for node {processNode.ElementId}");
                        break;
                }

                Console.WriteLine($"Activity {processNode.ElementId} completed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in main activity {processNode.ElementId}: {ex.Message}");
                throw;
            }
        }

        private async Task ExecuteUserTask(BpmnProcessNode processNode)
        {
            await UserTaskExecutor.ExecuteAsync(processNode, this);
        }

        private async Task ExecuteScriptTask(BpmnProcessNode processNode)
        {
            await ScriptExecutor.ExecuteAsync(processNode, this);
        }

        public async Task FindNextNodes(BpmnProcessNode processNode)
        {
            var element = DefinitionsHandler.GetElementById(processNode.ElementId);

            if (element is BpmnGateway gateway)
            {
                // Handle gateway logic
                IGatewayHandler? handler = gateway switch
                {
                    BpmnInclusiveGateway _ => new InclusiveGatewayHandler(),
                    BpmnExclusiveGateway _ => new ExclusiveGatewayHandler(),
                    BpmnParallelGateway _ => new ParallelGatewayHandler(),
                    _ => null
                };

                if (handler != null)
                {
                    await handler.HandleGateway(processNode, this);
                }
            }
            else
            {
                // Fetch attached boundary events
                foreach (var target in processNode.OutgoingFlows)
                {
                    var isExecutable = processNode.CanBeContinue;

                    var newNode = CreateNewNode(
                        DefinitionsHandler.GetElementById(target.targetRef),
                        Guid.NewGuid(),
                        isExecutable,
                        processNode,
                        target
                    );

                    EnqueueNext(newNode);
                    Console.WriteLine($"New node {newNode.ElementId} created and added to queue.");
                }

                // Expire the current node
                processNode.Expire();

                // Cancel timers if the activity completed successfully
                if (processNode.CanBeContinue)
                {
                    Console.WriteLine($"Timers for node {processNode.ElementId} have been canceled.");
                }

                // Store process state
                StoreProcessState();
            }
        }


        private void HandleNodeException(BpmnProcessNode processNode, Exception exception)
        {
            processNode.Exceptions.Add(exception.InnerException?.Message ?? exception.Message);
            Console.WriteLine($"Error in node {processNode.ElementId}: {exception.Message}");
        }
    }
}