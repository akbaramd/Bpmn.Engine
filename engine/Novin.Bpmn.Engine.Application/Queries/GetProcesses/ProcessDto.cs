using System.Text.Json.Nodes;
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
    JsonObject Variables
);

public record ProcessListDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public string ProcessBpmnId { get; init; } = default!;
    public ProcessState State { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}