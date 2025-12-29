namespace Novin.Bpmn.Engine.Application.Commands.SetProcessVariables;

public sealed class SetProcessVariablesResult
{
    public Guid ProcessId { get; init; }
    public IReadOnlyCollection<string> UpsertedKeys { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> RemovedKeys { get; init; } = Array.Empty<string>();
}

