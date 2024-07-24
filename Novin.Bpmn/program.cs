using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Novin.Bpmn;
using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Core;
using Novin.Bpmn.Executors.Abstracts;
using Novin.Bpmn.Handlers;
using Novin.Bpmn.Models;
using BpmnTask = Novin.Bpmn.BpmnTask;

class Program
{
    static async Task Main(string[] args)
    {
        // Define the path to the BPMN file
        string bpmnFilePath = "C:\\Users\\ahmadi.UR-NEZAM\\RiderProjects\\BpmnEngine\\Novin.Bpmn.Test\\Bpmn\\simple_inclusive.bpmn";
        string deploymentName = "SimpleInclusiveProcess";

        // Initialize the definition and process storage
        IBpmnDefinitionAccessor bpmnDefinitionAccessor = new InMemoryBpmnDefinitionAccessor();
        IBpmnProcessAccessor bpmnProcessAccessor = new InMemoryBpmnProcessAccessor();

        // Initialize user handler and task storage
        IBpmnUserAccessor bpmnUserAccessor = new InMemoryBpmnUserAccessor();
        IBpmnTaskAccessor bpmnTaskAccessor = new InMemoryBpmnTaskAccessor();

        // Initialize the BpmnEngine
        var engine = new BpmnEngine(bpmnUserAccessor, bpmnTaskAccessor, bpmnDefinitionAccessor, bpmnProcessAccessor);

        // Deploy the BPMN definition
        engine.DeployDefinition(bpmnFilePath, deploymentName);
        Console.WriteLine("BPMN definition deployed successfully.");

        // Create a new process from the deployed definition
        var processEngine = await engine.CreateProcessAsync(deploymentName);
        Console.WriteLine("BPMN process created successfully.");

        // Start the process
        var instance = await processEngine.StartProcess(); // Start without immediately processing all tasks
        Console.WriteLine("BPMN process started successfully.");

        // Check and handle user tasks
        await HandleUserTasks(engine,processEngine, instance);
    }

    static async Task HandleUserTasks(BpmnEngine engine, BpmnProcessEngine processEngine, BpmnProcessInstance instance)
    {
        while (true)
        {
            var userTasks = GetPendingUserTasks(instance);

            if (userTasks.Count == 0)
            {
                Console.WriteLine("No pending user tasks. Process completed.");
                break;
            }

            foreach (var userTask in userTasks)
            {
                   instance = await engine.CompleteTaskAsync(userTask.UserTask.TaskId);
                 
            }

            // Process next steps in the workflow after completing user tasks
            await HandleUserTasks(engine, processEngine, instance);
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
