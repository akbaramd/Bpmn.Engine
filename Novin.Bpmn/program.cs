using Novin.Bpmn.Test;

var filePath = "C:\\Users\\ahmadi.UR-NEZAM\\RiderProjects\\Novin.Bpmn\\Novin.Bpmn\\Bpmn\\diagram-variables.bpmn";
var engine = new BpmnEngine(filePath);
await engine.ExecuteProcessAsync();