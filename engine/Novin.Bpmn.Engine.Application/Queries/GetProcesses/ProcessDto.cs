using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Queries.GetProcesses;

/// <summary>
/// DTO for process instance information
/// </summary>
public record ProcessDto(
    Guid Id,
    string Name,
    Guid DeploymentId,
    string ProcessBpmnId,
    ProcessState State,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    IReadOnlyDictionary<string, string> Variables
);