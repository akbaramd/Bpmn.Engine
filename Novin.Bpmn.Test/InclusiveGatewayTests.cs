using Novin.Bpmn;
using Novin.Bpmn.Test;

public class InclusiveGatewayTests
{
    private readonly string _bpmnFilePath =
        "C:\\Users\\ahmadi.UR-NEZAM\\RiderProjects\\BpmnEngine\\Novin.Bpmn.Test\\Bpmn\\inclusive_fork_merge_test.bpmn";

    [Fact]
    public async Task TestInclusiveGatewayExecution()
    {
        // Create an instance of the BPMN engine with the given file path and dependencies
        var engine = new BpmnEngine(_bpmnFilePath);

        // Execute the process step-by-step
        await engine.StartProcess(false);
        Assert.Equal("Activity_1", engine.State.NodeQueue.First().Id);
        Assert.Single(engine.State.NodeQueue);

        await engine.StartProcess(false);
        Assert.Equal("Activity_2", engine.State.NodeQueue.First().Id);
        Assert.Single(engine.State.NodeQueue);

        await engine.StartProcess(false);
        Assert.Equal("Gateway_inclusive", engine.State.NodeQueue.First().Id);
        Assert.Single(engine.State.NodeQueue);

        await engine.StartProcess(false);

        Assert.Equal(new[] { "Activity_3_1", "Activity_3_2", "Activity_3_3" }, engine.State.NodeQueue.Select(x => x.Id).ToArray());
        Assert.Equal(new[] { "Activity_3_1", "Activity_3_2" }, engine.State.NodeQueue.Where(x=>x.IsExecutable).Select(x => x.Id).ToArray());
        
        await engine.StartProcess(false);
        Assert.Equal(new[] {  "Activity_3_2", "Activity_3_3", "Gateway_inclusive2" }, engine.State.NodeQueue.Select(x => x.Id).ToArray());
        Assert.Equal(new[] { "Activity_3_2", "Gateway_inclusive2" }, engine.State.NodeQueue.Where(x=>x.IsExecutable).Select(x => x.Id).ToArray());
        
        await engine.StartProcess(false);
        Assert.Equal(new[] {  "Activity_3_3", "Gateway_inclusive2", "Gateway_inclusive2" }, engine.State.NodeQueue.Select(x => x.Id).ToArray());
        Assert.Equal(new[] { "Gateway_inclusive2", "Gateway_inclusive2" }, engine.State.NodeQueue.Where(x=>x.IsExecutable).Select(x => x.Id).ToArray());
        
        await engine.StartProcess(false);
        Assert.Equal(new[] { "Gateway_inclusive2", "Gateway_inclusive2", "Gateway_inclusive2"}, engine.State.NodeQueue.Select(x => x.Id).ToArray());
        Assert.Equal(new string[]{"Gateway_inclusive2", "Gateway_inclusive2", "Gateway_inclusive2"}, engine.State.NodeQueue.Where(x=>x.IsExecutable).Select(x => x.Id).ToArray());
        
        await engine.StartProcess(false);
        Assert.Equal(new[] { "Gateway_inclusive2", "Gateway_inclusive2"}, engine.State.NodeQueue.Select(x => x.Id).ToArray());
        Assert.Equal(new[]{"Gateway_inclusive2", "Gateway_inclusive2"}, engine.State.NodeQueue.Where(x=>x.IsExecutable).Select(x => x.Id).ToArray());
          
        await engine.StartProcess(false);
        Assert.Equal(new[] { "Gateway_inclusive2"}, engine.State.NodeQueue.Select(x => x.Id).ToArray());
        Assert.Equal(new[]{"Gateway_inclusive2"}, engine.State.NodeQueue.Where(x=>x.IsExecutable).Select(x => x.Id).ToArray());
        
        await engine.StartProcess(false);
        Assert.Equal(new[] { "Activity_4"}, engine.State.NodeQueue.Select(x => x.Id).ToArray());
        Assert.Equal(new string[]{"Activity_4"}, engine.State.NodeQueue.Where(x=>x.IsExecutable).Select(x => x.Id).ToArray());
        
        await engine.StartProcess(false);
        Assert.Equal(new[] { "Event_End"}, engine.State.NodeQueue.Select(x => x.Id).ToArray());
        Assert.Equal(new string[]{"Event_End"}, engine.State.NodeQueue.Where(x=>x.IsExecutable).Select(x => x.Id).ToArray());
    }
}