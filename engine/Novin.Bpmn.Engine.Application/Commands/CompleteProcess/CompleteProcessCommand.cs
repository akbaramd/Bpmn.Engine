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

public class CompleteProcessResult
{
    public Guid ProcessId { get; set; }
    public DateTime CompletedAt { get; set; }
    public bool Success { get; set; }
}

