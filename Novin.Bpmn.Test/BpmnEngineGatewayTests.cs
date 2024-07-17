using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Novin.Bpmn.Test;
using Novin.Bpmn.Test.Executors;
using Novin.Bpmn.Test.Executors.Abstracts;
using Novin.Bpmn.Test.Models;

public class BpmnEngineTests
{
    [Fact]
    public async Task TestBpmnEngineExecution()
    {
        // Arrange
        string bpmnFilePath = "C:\\Users\\ahmadi.UR-NEZAM\\RiderProjects\\BpmnEngine\\Novin.Bpmn.Test\\Bpmn\\parallel-merge.bpmn";

        ITaskExecutor scriptTaskExecutor = new ScriptTaskExecutor();
        IUserTaskExecutor userTaskExecutor = new UserTaskExecutor();
        IStartEventExecutor startEventExecutor = new StartEventExecutor();
        IEndEventExecutor endEventExecutor = new EndEventExecutor();
        IBpmnFileDeserializer fileDeserializer = new BpmnFileDeserializer();

        var engine = new BpmnEngine(
            bpmnFilePath,
            scriptTaskExecutor,
            userTaskExecutor,
            startEventExecutor,
            endEventExecutor,
            fileDeserializer
        );

        // Act & Assert

        // First execution: check if it reaches the start event
        var instance = await engine.ExecuteProcessAsync();
        var activeRoutes = instance.GetActiveRoutes();

        Assert.Contains(activeRoutes.Select(x=>x.Id).ToArray(), ["Activity_Start"]);

        // Second execution: check if it reaches the inclusive gateway
        instance = await engine.ExecuteProcessAsync();
        activeRoutes = instance.GetActiveRoutes();

        Assert.Contains(activeRoutes.Select(x=>x.Id).ToArray(), ["Gateway_inclusive"]);

        // Third execution: check if it reaches a specific activity
        instance = await engine.ExecuteProcessAsync();
        activeRoutes = instance.GetActiveRoutes();

        Assert.Contains(activeRoutes.Select(x=>x.Id).ToArray(), ["Activity_2_1","Activity_2_21"]);

    }
}
