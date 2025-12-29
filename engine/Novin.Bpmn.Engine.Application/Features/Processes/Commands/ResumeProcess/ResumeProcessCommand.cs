using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.ResumeProcess;

public sealed class ResumeProcessCommand : IRequest<ResumeProcessResult>
{
    public Guid ProcessId { get; }

    public ResumeProcessCommand(Guid processId)
    {
        ProcessId = processId != Guid.Empty
            ? processId
            : throw new ArgumentException("ProcessId cannot be empty", nameof(processId));
    }
}

