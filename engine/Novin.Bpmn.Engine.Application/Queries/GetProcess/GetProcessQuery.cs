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
    
    /// <summary>
    /// Base process state (Running, Suspended, Completed, etc.)
    /// </summary>
    public Domain.ValueObjects.ProcessState State { get; set; }
    
    /// <summary>
    /// Derived status that provides more detailed information than State.
    /// This is especially useful when State=Running but there are open incidents
    /// (indicates RunningWithIncidents - blocked but recoverable).
    /// </summary>
    public Domain.ValueObjects.ProcessDerivedStatus DerivedStatus { get; set; }
    
    public Dictionary<string, object> Variables { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

