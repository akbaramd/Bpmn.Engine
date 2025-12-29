namespace Novin.Bpmn.Engine.Application.Commands.CompleteProcess;

public class CompleteProcessResult
{
    public Guid ProcessId { get; set; }
    public DateTime CompletedAt { get; set; }
    public bool Success { get; set; }
}