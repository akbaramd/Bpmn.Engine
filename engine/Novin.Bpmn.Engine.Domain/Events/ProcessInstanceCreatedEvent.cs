using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Events;

/// <summary>
/// Raised when a new process instance is created (before it is started).
/// </summary>
public sealed class ProcessInstanceCreatedEvent : BaseDomainEvent
{
    public Guid ProcessId { get; }
    public Guid DeploymentId { get; }
    public Guid ProjectId { get; }
    public string ProcessDefinitionId { get; }
    public string? BusinessKey { get; }
    public IReadOnlyDictionary<string, string> InitialVariables { get; }
    public DateTime CreatedAtUtc { get; }

    public ProcessInstanceCreatedEvent(
        Guid processId,
        Guid deploymentId,
        Guid projectId,
        string processDefinitionId,
        string? businessKey,
        IReadOnlyDictionary<string, string> initialVariables,
        DateTime createdAtUtc)
    {
        ProcessId = processId;
        DeploymentId = deploymentId;
        ProjectId = projectId;
        ProcessDefinitionId = processDefinitionId;
        BusinessKey = businessKey;
        InitialVariables = initialVariables;
        CreatedAtUtc = createdAtUtc;
    }
}

