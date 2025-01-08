using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Novin.Bpmn;
using Novin.Bpmn.Core;
using Novin.Bpmn.Executors;
using Novin.Bpmn.Handlers;
using Novin.Bpmn.V2.Handlers;
using Xunit;

public class BpmnProcessEngineTest
{
    private readonly string _bpmnFilePath =
        "C:\\Users\\ahmadi.UR-NEZAM\\RiderProjects\\BpmnEngine\\Novin.Bpmn.Test\\Bpmn\\simple_inclusive.bpmn";

    [Fact]
    public async Task TestBpmnEngineStepByStepExecution()
    {
        var scriptHandler = new ScriptHandler();
        // Load BPMN definition XML
        var definitionXml = File.ReadAllText(_bpmnFilePath);
        var definitionsHandler = new BpmnDefinitionsHandler(definitionXml);

        // Initialize necessary components
        var router = new BpmnV2Router(
            new BpmnV2ExclusiveGatewayHandler(scriptHandler),
            new BpmnV2InclusiveGatewayHandler(scriptHandler),
            new BpmnV2ParallelGatewayHandler()
        );
        var taskHandler = new Bpmn2TaskHandler(
            new Bpmn2ScriptTaskHandler());
        var boundaryEventHandler = new BpmnV2BoundaryEventHandler();

        // Create the BPMN process instance and executor
        var engine = new BpmnV2ProcessExecutor(router, taskHandler, boundaryEventHandler,new InMemoryBpmnProcessAccessor());
        engine.Initialize("test",definitionXml);

        // Create a CancellationTokenSource and pass its token
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        try
        {
            // Final step
            await engine.StartProcessAsync(false, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Process was canceled.");
        }

        
        // Ensure the file exists
        Assert.True(File.Exists(_bpmnFilePath));
    }
}
