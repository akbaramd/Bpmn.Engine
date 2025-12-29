using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.SuspendProcess;

public sealed class SuspendProcessCommand : IRequest<SuspendProcessResult>
{
    public Guid ProcessId { get; }
    public string? Reason { get; }

    public SuspendProcessCommand(Guid processId, string? reason = null)
    {
        ProcessId = processId != Guid.Empty
            ? processId
            : throw new ArgumentException("ProcessId cannot be empty", nameof(processId));
        Reason = reason;
    }
}

