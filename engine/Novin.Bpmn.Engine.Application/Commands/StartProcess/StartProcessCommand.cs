using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.StartProcess;

public class StartProcessCommand : IRequest<StartProcessResult>
{
    public string ProcessDefinitionId { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public Dictionary<string, object>? InitialVariables { get; set; }

    public StartProcessCommand(string processDefinitionId, string processName, Dictionary<string, object>? initialVariables = null)
    {
        ProcessDefinitionId = processDefinitionId;
        ProcessName = processName;
        InitialVariables = initialVariables;
    }
}

public class StartProcessResult
{
    public Guid ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime StartedAt { get; set; }
}

