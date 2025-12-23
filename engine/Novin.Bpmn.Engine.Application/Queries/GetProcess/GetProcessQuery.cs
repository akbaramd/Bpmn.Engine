using MediatR;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.Queries.GetProcess;

public class GetProcessQuery : IRequest<ProcessDto?>
{
    public Guid ProcessId { get; set; }

    public GetProcessQuery(Guid processId)
    {
        ProcessId = processId;
    }
}

public class ProcessDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProcessDefinitionId { get; set; } = string.Empty;
    public Domain.ValueObjects.ProcessState State { get; set; }
    public Dictionary<string, object> Variables { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

