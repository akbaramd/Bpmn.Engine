using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Common.Interfaces;

/// <summary>
/// Catalog service for BPMN process definitions.
/// Handles compilation, caching, and memory store management.
/// </summary>
public interface IExecutableDefinitionCatalog
{
    /// <summary>
    /// Get a compiled definition (memory-first, then DB, then compile).
    /// </summary>
    Task<ExecutableProcessDefinition> GetAsync(ProcessDefinitionRef defRef, CancellationToken ct);

    /// <summary>
    /// Warm-up all active definitions (for startup).
    /// </summary>
    Task WarmUpAllAsync(CancellationToken ct);

    /// <summary>
    /// Handle definition change (invalidate cache and reload).
    /// </summary>
    Task OnDefinitionChangedAsync(ProcessDefinitionRef defRef, CancellationToken ct);
}

