public class BpmnProcessEngineTest
{
    private readonly string _bpmnFilePath = "C:\\Users\\ahmadi.UR-NEZAM\\RiderProjects\\BpmnEngine\\Novin.Bpmn.Test\\Bpmn\\simple_inclusive.bpmn";

    [Fact]
    public async Task TestBpmnEngineStepByStepExecution()
    {
        // Create an instance of the BPMN engine with the given file path and dependencies
        var engine = new BpmnProcessEngine(_bpmnFilePath);

        // Execute the process step-by-step
        await engine.StartProcess(false);
        Assert.Single(engine.processState.NodeQueue);

        await engine.StartProcess(false);
        Assert.Single(engine.processState.NodeQueue);

        await engine.StartProcess(false);
        Assert.Single(engine.processState.NodeQueue);

        await engine.StartProcess(false);
        Assert.Equal(3,engine.processState.NodeQueue.Count);
        
        await engine.StartProcess(false);
    }

}
