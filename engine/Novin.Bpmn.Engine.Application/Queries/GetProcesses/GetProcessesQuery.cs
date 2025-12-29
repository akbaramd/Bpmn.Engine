using MediatR;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Queries.GetProcesses;

/// <summary>
/// Query to get process instances with optional filtering and pagination
/// </summary>
public record GetProcessesQuery(
    ProcessState? State = null,
    Guid? DeploymentId = null,
    string? ProcessBpmnId = null,
    int Skip = 0,
    int Take = 50
) : IRequest<IEnumerable<ProcessDto>>;