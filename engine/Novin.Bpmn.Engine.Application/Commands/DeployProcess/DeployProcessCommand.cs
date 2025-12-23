using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.DeployProcess;

public class DeployProcessCommand : IRequest<DeployProcessResult>
{
    public string DeploymentKey { get; set; } = string.Empty;
    public string BpmnXml { get; set; } = string.Empty;
    public string? Label { get; set; }

    public DeployProcessCommand(
        string deploymentKey,
        string bpmnXml,
        string? label = null)
    {
        DeploymentKey = deploymentKey;
        BpmnXml = bpmnXml;
        Label = label;
    }
}

public class DeployProcessResult
{
    public Guid DeploymentId { get; set; }
    public string DeploymentKey { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime DeployedAt { get; set; }
    public bool IsNewDeployment { get; set; }
}

