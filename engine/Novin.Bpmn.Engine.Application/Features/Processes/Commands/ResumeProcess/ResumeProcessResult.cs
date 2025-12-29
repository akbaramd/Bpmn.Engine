namespace Novin.Bpmn.Engine.Application.Commands.ResumeProcess;

public sealed class ResumeProcessResult
{
    public Guid ProcessId { get; init; }
    public DateTime ResumedAt { get; init; }
}

