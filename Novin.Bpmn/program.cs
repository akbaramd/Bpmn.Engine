// Define the path to the BPMN file

using Novin.Bpmn;
using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Storage;

string bpmnFilePath = "C:\\Users\\ahmadi.UR-NEZAM\\RiderProjects\\BpmnEngine\\Novin.Bpmn.Test\\Bpmn\\simple_inclusive.bpmn";
string deploymentName = "SimpleInclusiveProcess";
string version = "1.0.0";

// Initialize the definition and process storage
IDefinitionAccessor definitionAccessor = new BpmnInMemoryDefinitionAccessor();
IProcessAccsessor processAccsessor = new BpmnInMemoryProcessAccsessor();

// Initialize user handler and task storage (implement these interfaces as needed)
IUserAccessor userAccessor = new BpmnInMemoryUserAccessor();
ITaskStorage taskStorage = new InMemoryTaskStorage();

// Initialize the BpmnEngine
BpmnEngine engine = new BpmnEngine(userAccessor, taskStorage, definitionAccessor, processAccsessor);

// Deploy the BPMN definition
engine.DeployDefinition(bpmnFilePath, deploymentName, version);
Console.WriteLine("BPMN definition deployed successfully.");

// Create a new process from the deployed definition
var processEngine = await engine.CreateProcessAsync(deploymentName, version);
Console.WriteLine("BPMN process created successfully.");

// Start the process
await processEngine.StartProcess();
Console.WriteLine("BPMN process started successfully.");