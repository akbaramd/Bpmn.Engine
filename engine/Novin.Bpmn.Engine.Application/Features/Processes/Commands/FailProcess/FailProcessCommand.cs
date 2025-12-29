using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.FailProcess;

public sealed class FailProcessCommand : IRequest<FailProcessResult>
{
    public Guid ProcessId { get; }
    public string Error { get; }

    public FailProcessCommand(Guid processId, string error)
    {
        if (processId == Guid.Empty)
            throw new ArgumentException("ProcessId cannot be empty", nameof(processId));
        if (string.IsNullOrWhiteSpace(error))
            throw new ArgumentException("Error cannot be empty", nameof(error));

        ProcessId = processId;
        Error = error;
    }
}

