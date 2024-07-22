using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Novin.Bpmn;
using Novin.Bpmn.Abstractions;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Define the BPMN file path
        string bpmnFilePath = "C:\\Users\\ahmadi.UR-NEZAM\\RiderProjects\\BpmnEngine\\Novin.Bpmn.Test\\Bpmn\\simple_inclusive.bpmn";
        // string bpmnFilePath = "D:\\Projects\\Company\\AkbarAhmadiSaray\\Bomn\\Bpmn.Engine\\Novin.Bpmn.Test\\Bpmn\\simple_inclusive.bpmn";

        // Create an instance of the BPMN engine with the given file path and dependencies
        var engine = new BpmnEngine(bpmnFilePath , processId:"process");

        // Start the timer
        Stopwatch stopwatch = Stopwatch.StartNew();

        // Execute the process asynchronously
        await engine.StartProcess();

        var res = engine.GetExecutedPathsWithFlows();
        // Complete user tasks if any
        while (engine.State.WaitingUserTasks.Any())
        {
            foreach (var userTask in engine.State.WaitingUserTasks)
            {
                await engine.CompleteUserTask(userTask.Value.ElementId);
                await engine.StartProcess();
            }
        }

        // Stop the timer
        stopwatch.Stop();

        return;

    }

  
}



class TestServiceHandler : IServiceTaskHandler
{
    public Task HandleAsync(BpmnState state)
    {
        state.Variables.Index = 1;
        Console.WriteLine(state);
        return Task.CompletedTask;;
    }
}
