using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Novin.Bpmn;
using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Core;
using Novin.Bpmn.Executors;
using Novin.Bpmn.Executors.Abstracts;
using Novin.Bpmn.Handlers;
using Novin.Bpmn.Models;

class Program
{
    static async Task Main(string[] args)
    {
        // Define the path to the BPMN file and deployment name
        const string bpmnFilePath = "C:\\Users\\ahmadi.UR-NEZAM\\RiderProjects\\BpmnEngine\\Novin.Bpmn.Test\\Bpmn\\simple_inclusive.bpmn";
        const string deploymentName = "SimpleInclusiveProcess";

        // Initialize storage and accessors
        IBpmnDefinitionAccessor bpmnDefinitionAccessor = new InMemoryBpmnDefinitionAccessor();
        IBpmnProcessAccessor bpmnProcessAccessor = new InMemoryBpmnProcessAccessor();
        IBpmnTaskAccessor bpmnTaskAccessor = new InMemoryBpmnTaskAccessor();

        // Initialize executors
        IScriptTaskExecutor scriptExecutor = new ScriptTaskExecutor();
        IUserTaskExecutor userTaskExecutor = new UserTaskExecutor(bpmnTaskAccessor);
        IServiceTaskExecutor serviceTaskExecutor = new ServiceTaskExecutor();

        // Initialize the BpmnEngine with executors
        var engine = new BpmnEngine(
            bpmnTaskAccessor,
            bpmnDefinitionAccessor,
            bpmnProcessAccessor,
            scriptExecutor,
            userTaskExecutor,
            serviceTaskExecutor);

        // Deploy the BPMN definition
        engine.DeployDefinition(bpmnFilePath, deploymentName);
        Console.WriteLine("BPMN definition deployed successfully.");

        // Create and start the process
        var processEngine = await engine.CreateProcessAsync(deploymentName);
        Console.WriteLine("BPMN process created successfully.");
        var instance = await processEngine.StartProcess();
        Console.WriteLine("BPMN process started successfully.");

        // Handle user tasks
        await HandleUserTasks(engine, processEngine, instance);
    }

    static async Task HandleUserTasks(BpmnEngine engine, BpmnProcessEngine processEngine, BpmnProcessInstance instance)
    {
        while (true)
        {
            // Retrieve all pending user tasks
            var userTasks = GetPendingUserTasks(instance);

            if (userTasks.Count == 0)
            {
                Console.WriteLine("No pending user tasks. Process completed.");
                break;
            }

            Console.WriteLine($"Found {userTasks.Count} pending user task(s).");

            foreach (var userTaskNode in userTasks)
            {
                Console.WriteLine($"Completing task: {userTaskNode.UserTask?.TaskId} ({userTaskNode.UserTask?.Name})");

                // Simulate completing the user task
                instance = await engine.CompleteTaskAsync(userTaskNode.UserTask!.TaskId);
                Console.WriteLine($"Task {userTaskNode.UserTask.TaskId} completed.");
            }

            // Process the next steps in the workflow after completing user tasks
            instance = await processEngine.StartProcess();
        }
    }

    static List<BpmnProcessNode> GetPendingUserTasks(BpmnProcessInstance instance)
    {
        var userTasks = new List<BpmnProcessNode>();

        foreach (var node in instance.NodeStack)
        {
            if (node.UserTask != null && !node.UserTask.IsCompleted)
            {
                userTasks.Add(node);
            }
        }

        return userTasks;
    }
}
