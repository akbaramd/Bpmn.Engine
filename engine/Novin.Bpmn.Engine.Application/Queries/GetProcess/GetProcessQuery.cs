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