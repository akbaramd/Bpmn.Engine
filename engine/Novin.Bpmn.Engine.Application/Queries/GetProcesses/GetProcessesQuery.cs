using MediatR;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Queries.GetProcesses;

/// <summary>
/// Query to get process instances with optional filtering and pagination
/// </summary>
public record GetProcessesQuery(
    ProcessState? State = null,
    string? ProcessDefinitionId = null,
    int Skip = 0,
    int Take = 50
) : IRequest<IEnumerable<ProcessDto>>;

/// <summary>
/// DTO for process instance information
/// </summary>
public record ProcessDto(
    Guid Id,
    string Name,
    string ProcessDefinitionId,
    ProcessState State,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    IReadOnlyDictionary<string, object> Variables
);