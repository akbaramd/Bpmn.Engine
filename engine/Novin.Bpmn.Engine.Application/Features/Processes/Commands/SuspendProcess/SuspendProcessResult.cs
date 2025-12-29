namespace Novin.Bpmn.Engine.Application.Commands.SuspendProcess;

public sealed class SuspendProcessResult
{
    public Guid ProcessId { get; init; }
    public DateTime SuspendedAt { get; init; }
    public string? Reason { get; init; }
}

