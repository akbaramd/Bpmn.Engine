using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.CompleteProcess;

public class CompleteProcessCommand : IRequest<CompleteProcessResult>
{
    public Guid ProcessId { get; set; }

    public CompleteProcessCommand(Guid processId)
    {
        ProcessId = processId;
    }
}