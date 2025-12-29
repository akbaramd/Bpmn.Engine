using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.SetProcessVariables;

public sealed class SetProcessVariablesCommand : IRequest<SetProcessVariablesResult>
{
    public Guid ProcessId { get; }
    public IDictionary<string, object?> Upserts { get; }
    public IReadOnlyCollection<string> Removals { get; }

    public SetProcessVariablesCommand(
        Guid processId,
        IDictionary<string, object?>? upserts = null,
        IEnumerable<string>? removals = null)
    {
        ProcessId = processId != Guid.Empty
            ? processId
            : throw new ArgumentException("ProcessId cannot be empty", nameof(processId));

        Upserts = upserts ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        Removals = removals?.Where(r => !string.IsNullOrWhiteSpace(r)).ToArray()
                   ?? Array.Empty<string>();
    }
}

