namespace Novin.Bpmn.Engine.Application.Commands.FailProcess;

public sealed class FailProcessResult
{
    public Guid ProcessId { get; init; }
    public DateTime FailedAt { get; init; }
    public string Error { get; init; } = string.Empty;
}

