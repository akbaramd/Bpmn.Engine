namespace Novin.Bpmn.Engine.Application.Commands.TerminateProcess;

public sealed class TerminateProcessResult
{
    public Guid ProcessId { get; init; }
    public DateTime TerminatedAt { get; init; }
    public string? Reason { get; init; }
}

