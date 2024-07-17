using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Novin.Bpmn.Test;
using Novin.Bpmn.Test.Models;

public class BpmnEngineTests
{
    private readonly string _bpmnFilePath;

    public BpmnEngineTests()
    {
        // Set the path to your BPMN XML file
        _bpmnFilePath = "D:\\Projects\\Github\\Bpmn.Engine\\Novin.Bpmn.Test\\Bpmn\\diagram_1.bpmn";
    }

    [Fact]
    public async Task TestBpmnEngineExecution()
    {
        // Arrange
        var engine = new BpmnEngine(_bpmnFilePath);

        // Capture console output
        using (var consoleOutput = new ConsoleOutput())
        {
            // Act
            var instance = await engine.ExecuteProcessAsync();

            // Assert
            var output = consoleOutput.GetOutput();
            Assert.Contains("Way 1", output);
            Assert.Contains("Way 2", output);
            Assert.Contains("Way 1-2", output);
            Assert.Contains("Way 1-1", output);
            Assert.Contains("After parallel_2", output);
            Assert.Contains("Process completed.", output);
        }
    }
}

