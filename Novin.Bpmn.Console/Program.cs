using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing;
using Novin.Bpmn.EventSourcing.Core.Executions;
using Novin.Bpmn.EventSourcing.Core.Process;
using Novin.Bpmn.EventSourcing.Core.Services;

class Program
{
    static async Task Main(string[] args)
    {
        using IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            })
            .ConfigureServices((context, services) =>
            {
                services.AddBpmnEngine();
                services.AddScoped<IExecutionPathService, ExecutionPathService>();
            })
            .Build();

        await host.StartAsync();

        var filePath = Path.Combine(AppContext.BaseDirectory, "Bpmn", "diagram_2.bpmn");
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"File not found: {filePath}");
            return;
        }

        var bpmnXml = await File.ReadAllTextAsync(filePath);

        var deploymentService = host.Services.GetRequiredService<IDeploymentService>();
        var deploymentKey = "ConsoleAppTestDeployment";

        var deployment = deploymentService.Deploy(deploymentKey, bpmnXml);
        Console.WriteLine($"📦 Deployed process with key: {deployment.DeploymentKey}");

        var processEngine = host.Services.GetRequiredService<IProcessEngine>();
        var pathService = host.Services.GetRequiredService<IExecutionPathService>();
        var processStateStore = host.Services.GetRequiredService<IProcessStateStore>();

        var res = await processEngine.StartProcessAsync(deployment.DeploymentKey, "start_process", new Dictionary<string, object?>
        {
            ["num1"] = 3,
            ["num2"] = 2,
            ["operator"] = "sum",
        });

        Console.WriteLine($"🚀 Process started with InstanceId: {res.InstanceId}");

        // ⏳ Wait until process completes
        bool completed = false;
        for (int i = 0; i < 60; i++)
        {
            var state = processStateStore.Get(res.InstanceId);
            if (state?.Status == ProcessStateStatus.Completed)
            {
                completed = true;
                break;
            }

            Console.WriteLine("⏱️  Process not completed yet... waiting 500ms");
            await Task.Delay(500);
        }

        Console.WriteLine(completed
            ? "✅ Process completed successfully!"
            : "⚠️ Process did not complete in expected time.");

        // 📜 Story-like explanation of execution paths
        var map = pathService.BuildExecutionTraces(res.InstanceId);
        ExecutionStoryNarrator.ExplainSequentialFlows(map);

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();

        await host.StopAsync();
    }

   
}


public static class ExecutionStoryNarrator
{
    public static void ExplainSequentialFlows(ExecutionTraceMap traceMap)
    {
        Console.WriteLine($"Narrative execution path for instance: {traceMap.InstanceId}");
        Console.WriteLine(new string('=', 80));

        var traces = traceMap.Traces.ToDictionary(t => t.ExecutionId.ToString());
        var childMap = traceMap.Traces
            .Where(t => t.ParentExecutionId != null)
            .GroupBy(t => t.ParentExecutionId!)
            .ToDictionary(g => g.Key, g => g.ToList());

        var roots = traceMap.Traces
            .Where(t => t.ParentExecutionId == null)
            .OrderBy(t => t.ExecutionId)
            .ToList();

        foreach (var root in roots)
        {
            DescribeFlowRecursive(root, childMap, traces);
        }

        Console.WriteLine(new string('=', 80));
    }

    private static void DescribeFlowRecursive(
        ExecutionTrace current,
        Dictionary<string, List<ExecutionTrace>> childMap,
        Dictionary<string, ExecutionTrace> allTraces,
        int depth = 0)
    {
        var indent = new string(' ', depth * 4);

        Console.WriteLine($"{indent}Started new execution path: {current.ExecutionId}");

        if (!current.IsExecutable)
        {
            Console.WriteLine($"{indent}  This path was not executable and will not continue.");
            return;
        }

        if (current.Path.Count == 0)
        {
            Console.WriteLine($"{indent}  No elements visited.");
        }
        else
        {
            for (int i = 0; i < current.Path.Count; i++)
            {
                var element = current.Path[i];
                if (i == 0)
                    Console.WriteLine($"{indent}  Begins at: {element}");
                else
                    Console.WriteLine($"{indent}  Then goes to: {element}");
            }
        }

        Console.WriteLine($"{indent}  Final state of this execution: {current.State}");

        if (childMap.TryGetValue(current.ExecutionId.ToString(), out var children))
        {
            Console.WriteLine($"{indent}  This path forks into {children.Count} branch(es):");
            foreach (var child in children)
            {
                DescribeFlowRecursive(child, childMap, allTraces, depth + 1);
            }
        }
        else
        {
            Console.WriteLine($"{indent}  This path ends here.");
        }
    }
}

