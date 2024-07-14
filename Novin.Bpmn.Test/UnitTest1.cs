using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test;

public class BpmnEngineTests
{
    private const string SampleBpmnFilePath = "C:\\Users\\ahmadi.UR-NEZAM\\RiderProjects\\BpmnEngine\\Novin.Bpmn.Test\\Bpmn\\diagram-variables.bpmn";

    private BpmnEngine CreateEngine(string filePath)
    {
        return new BpmnEngine(filePath);
    }

    [Fact]
    public void TestDeserializeBpmnFile()
    {
        var engine = CreateEngine(SampleBpmnFilePath);
        Assert.NotNull(engine.Instance.BpmnDefinitions);
        Assert.NotEmpty(engine.Instance.BpmnDefinitions.Items);
    }

    [Fact]
    public void TestFindStartNode()
    {
        var engine = CreateEngine(SampleBpmnFilePath);
        var startNode = engine.FindStartNode();
        Assert.NotNull(startNode);
        Assert.IsType<BpmnStartEvent>(startNode);
    }

 

    [Fact]
    public void TestExecuteProcessFlow()
    {
        var engine = CreateEngine(SampleBpmnFilePath);
        engine.ExecuteProcess();
        Assert.NotNull(engine.Instance.CurrentNodeId);
    }

    [Fact]
    public void TestCompleteUserTask()
    {
        var engine = CreateEngine(SampleBpmnFilePath);
        engine.ExecuteProcess(); // Should reach the user task
        var userTask = engine.Instance.PendingUserTask;
        if (userTask != null)
        {
            engine.CompleteUserTask("user1", userTask.id);
            Assert.NotNull(engine.Instance.CurrentNodeId);
        }
    }

    [Fact]
    public void TestRollback()
    {
        var engine = CreateEngine(SampleBpmnFilePath);
        engine.ExecuteProcess(); // Should reach the user task
        engine.Rollback();
        Assert.NotNull(engine.Instance.CurrentNodeId);
    }

    [Fact]
    public void TestVariables()
    {
        var engine = CreateEngine(SampleBpmnFilePath);
        dynamic variables = engine.Instance.Variables;
        variables.testVariable = "testValue";
        Assert.Equal("testValue", variables.testVariable);
    }

    [Fact]
    public void TestHistoryClearing()
    {
        var engine = CreateEngine(SampleBpmnFilePath);
        engine.ExecuteProcess();
        engine.Instance.ClearHistory();
        Assert.Empty(engine.Instance.History);
    }
}