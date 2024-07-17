using Novin.Bpmn.Test;
using Novin.Bpmn.Test.Core;
using Novin.Bpmn.Test.Executors;
using Novin.Bpmn.Test.Executors.Abstracts;
using Novin.Bpmn.Test.Models;
using System.Threading.Tasks;

// Define the BPMN file path
string bpmnFilePath = "C:\\Users\\ahmadi.UR-NEZAM\\RiderProjects\\BpmnEngine\\Novin.Bpmn.Test\\Bpmn\\parallel-merge.bpmn";

// Create the necessary executors and deserializer
ITaskExecutor scriptTaskExecutor = new ScriptTaskExecutor();
IUserTaskExecutor userTaskExecutor = new UserTaskExecutor();
IStartEventExecutor startEventExecutor = new StartEventExecutor();
IEndEventExecutor endEventExecutor = new EndEventExecutor();
IBpmnFileDeserializer fileDeserializer = new BpmnFileDeserializer();


// Create an instance of the BPMN engine with the given file path and dependencies
var engine = new BpmnEngine(
    bpmnFilePath,
    scriptTaskExecutor,
    userTaskExecutor,
    startEventExecutor,
    endEventExecutor,
    fileDeserializer
);

// Execute the process asynchronously
await engine.ExecuteProcessAsync();