using Novin.Bpmn.Test;

    string bpmnFilePath = "D:\\Projects\\Github\\Bpmn.Engine\\Novin.Bpmn.Test\\Bpmn\\parallel-merge.bpmn";

// Create an instance of the BPMN engine with the given file path
var engine = new BpmnEngine(bpmnFilePath);

// Execute the process
await engine.ExecuteProcessAsync();

