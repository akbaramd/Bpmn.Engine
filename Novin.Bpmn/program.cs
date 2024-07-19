using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Novin.Bpmn.Test;
using Novin.Bpmn.Test.Core;
using Novin.Bpmn.Test.Executors;
using Novin.Bpmn.Test.Executors.Abstracts;
using Novin.Bpmn.Test.Models;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Define the BPMN file path
        string bpmnFilePath = "D:\\Projects\\Company\\AkbarAhmadiSaray\\Bomn\\Bpmn.Engine\\Novin.Bpmn.Test\\Bpmn\\simple_inclusive.bpmn";

        // Create an instance of the BPMN engine with the given file path and dependencies
        var engine = new BpmnEngine(bpmnFilePath);

        // Start the timer
        Stopwatch stopwatch = Stopwatch.StartNew();

        // Execute the process asynchronously
        await engine.StartProcess();

        // Complete user tasks if any
        while (engine.State.WaitingUserTasks.Any())
        {
            foreach (var userTask in engine.State.WaitingUserTasks)
            {
                await engine.CompleteUserTask(userTask.Value.Id);
            }
        }

        // Stop the timer
        stopwatch.Stop();

        // Print BPMN node history schematic
        PrintBpmnNodeHistory(engine.State);

        // Generate and print execution report
        GenerateExecutionReport(engine.State, stopwatch.Elapsed);

        // Convert the state to JSON after execution
        var stateJson = JsonSerializer.Serialize(engine.State, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(stateJson);
    }

    private static void PrintBpmnNodeHistory(ProcessState state)
    {
        var visitedNodes = new HashSet<string>();
        foreach (var node in state.Nodes.Values)
        {
            if (visitedNodes.Contains(node.Id)) continue;

            Console.WriteLine($"Node ID: {node.Id}");
            PrintNodeInstances(node);
            PrintOutgoingFlows(node);
            Console.WriteLine(new string('-', 50)); // Line divider
            visitedNodes.Add(node.Id);
        }
    }

    private static void PrintNodeInstances(BpmnNode node)
    {
        foreach (var instance in node.Instances)
        {
            Console.WriteLine($"  Instance Timestamp: {instance.Timestamp}, IsExecutable: {instance.IsExecutable}, IsExpired: {instance.IsExpired}");
            if (instance.Tokens.Any())
            {
                Console.WriteLine("    Tokens: " + string.Join(", ", instance.Tokens));
            }
            if (instance.Merges.Any())
            {
                Console.WriteLine("    Merges: " + string.Join(", ", instance.Merges));
            }
        }
    }

    private static void PrintOutgoingFlows(BpmnNode node)
    {
        if (node.OutgoingFlows.Any())
        {
            Console.WriteLine("  Outgoing Flows:");
            foreach (var flow in node.OutgoingFlows)
            {
                Console.WriteLine($"    -> Flow to Node ID: {flow}");
            }
        }
    }

    private static void GenerateExecutionReport(ProcessState state, TimeSpan elapsedTime)
    {
        int totalNodes = state.Nodes.Count;
        int executedNodes = state.Nodes.Values.SelectMany(x=>x.Instances).Count();

        Console.WriteLine(new string('=', 50));
        Console.WriteLine("Execution Report:");
        Console.WriteLine(new string('=', 50));

        Console.WriteLine($"Total Nodes: {totalNodes}");
        Console.WriteLine($"Executed Nodes: {executedNodes}");
        Console.WriteLine("Gateways by Type:");
        Console.WriteLine($"Total Time Elapsed: {elapsedTime}");
    }
}
