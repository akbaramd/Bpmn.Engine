namespace Novin.Bpmn.Engine.Application.Commands.CreateProcessInstance;

public sealed class CreateProcessInstanceResult
{
    public Guid ProcessId { get; init; }
    public string ProcessName { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

