using Novin.Bpmn.Test;

var filePath = "C:\\Users\\ahmadi.UR-NEZAM\\RiderProjects\\Novin.Bpmn\\Novin.Bpmn\\Bpmn\\diagram-variables.bpmn";
var engine = new BpmnEngine(filePath);
engine.ExecuteProcess();

// Simulate user completing the task
Console.WriteLine("Press any key to complete the user task...");
Console.ReadKey();
engine.CompleteUserTask("userId", "userTaskId");
