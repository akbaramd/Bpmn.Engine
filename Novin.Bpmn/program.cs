using Novin.Bpmn.Test;
using Novin.Bpmn.Test.Core;
using Novin.Bpmn.Test.Executors;
using Novin.Bpmn.Test.Executors.Abstracts;
using Novin.Bpmn.Test.Models;
using System.Threading.Tasks;

// Define the BPMN file path
string bpmnFilePath = "C:\\Users\\ahmadi.UR-NEZAM\\RiderProjects\\BpmnEngine\\Novin.Bpmn.Test\\Bpmn\\simple_inclusive.bpmn";


// Create an instance of the BPMN engine with the given file path and dependencies
var engine = new ProcessEngine(bpmnFilePath);

// Execute the process asynchronously
await engine.StartProcess(false);
await engine.StartProcess(false);
await engine.StartProcess(false);
await engine.StartProcess(false);
await engine.StartProcess(false);

