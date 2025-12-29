using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.TerminateProcess;

public sealed class TerminateProcessCommand : IRequest<TerminateProcessResult>
{
    public Guid ProcessId { get; }
    public string? Reason { get; }

    public TerminateProcessCommand(Guid processId, string? reason = null)
    {
        ProcessId = processId != Guid.Empty
            ? processId
            : throw new ArgumentException("ProcessId cannot be empty", nameof(processId));
        Reason = reason;
    }
}

