using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.StartProcess;

public class StartProcessCommand : IRequest<StartProcessResult>
{
    public Guid? ProcessId { get; set; }
    public Guid DeploymentId { get; set; }
    public string ProcessBpmnId { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public string? BusinessKey { get; set; }
    public Dictionary<string, string>? InitialVariables { get; set; }

    // Parameterless constructor for JSON deserialization
    public StartProcessCommand()
    {
    }

    public StartProcessCommand(
        Guid deploymentId,
        string processBpmnId,
        string processName,
        Dictionary<string, string>? initialVariables = null,
        string? businessKey = null)
    {
        DeploymentId = deploymentId;
        ProcessBpmnId = processBpmnId;
        ProcessName = processName;
        InitialVariables = initialVariables;
        BusinessKey = businessKey;
    }

    public StartProcessCommand(Guid processId)
    {
        ProcessId = processId;
    }
}