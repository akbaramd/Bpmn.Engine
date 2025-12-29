using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.CreateProcessInstance;

public sealed class CreateProcessInstanceCommand : IRequest<CreateProcessInstanceResult>
{
    public Guid DeploymentId { get; }
    public string ProcessBpmnId { get; }
    public string ProcessName { get; }
    public string? BusinessKey { get; }
    public IDictionary<string, object?>? InitialVariables { get; }

    public CreateProcessInstanceCommand(
        Guid deploymentId,
        string processBpmnId,
        string processName,
        IDictionary<string, object?>? initialVariables = null,
        string? businessKey = null)
    {
        if (deploymentId == Guid.Empty)
            throw new ArgumentException("DeploymentId cannot be empty.", nameof(deploymentId));
        if (string.IsNullOrWhiteSpace(processBpmnId))
            throw new ArgumentException("ProcessBpmnId cannot be empty.", nameof(processBpmnId));
        if (string.IsNullOrWhiteSpace(processName))
            throw new ArgumentException("ProcessName cannot be empty.", nameof(processName));

        DeploymentId = deploymentId;
        ProcessBpmnId = processBpmnId;
        ProcessName = processName;
        InitialVariables = initialVariables;
        BusinessKey = businessKey;
    }
}

