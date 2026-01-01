using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Common.Interfaces;

/// <summary>
/// In-memory store for compiled BPMN process definitions.
/// All runtime code should use this instead of parsing XML or querying DB.
/// </summary>
public interface IBpmnDefinitionMemoryStore
{
    /// <summary>
    /// Try to get a compiled definition from memory.
    /// </summary>
    bool TryGet(ProcessDefinitionRef defRef, out ExecutableProcessDefinition def);

    /// <summary>
    /// Warm-up: Load multiple definitions into memory (for startup).
    /// </summary>
    Task WarmUpAsync(IEnumerable<ProcessDefinitionRef> allRefs, CancellationToken ct);

    /// <summary>
    /// Set/update a definition in memory (for deploy/update).
    /// </summary>
    Task SetAsync(ExecutableProcessDefinition def, CancellationToken ct);

    /// <summary>
    /// Invalidate/remove a definition from memory (for deactivate/delete).
    /// </summary>
    Task InvalidateAsync(ProcessDefinitionRef defRef, CancellationToken ct);

    /// <summary>
    /// Get count of definitions in memory (for monitoring).
    /// </summary>
    int Count { get; }
}

