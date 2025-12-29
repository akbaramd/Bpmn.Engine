namespace Novin.Bpmn.Engine.Application.Commands.StartProcess;

public class StartProcessResult
{
    public Guid ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime StartedAt { get; set; }
}