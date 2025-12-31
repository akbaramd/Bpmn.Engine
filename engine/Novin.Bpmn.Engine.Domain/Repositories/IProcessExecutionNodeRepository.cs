// =======================================================
// 1) Repository Interface (Domain/Application contract)
// =======================================================
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.Common.Interfaces;

/// <summary>
/// Repository interface for NodeInstance aggregate.
/// NOTE: This interface matches the NEW NodeInstance model:
/// - ElementId
/// - CreatedAtUtc/StartedAtUtc/CompletedAtUtc
/// - State, ScopeId, ActivityInstanceId, ArrivedViaFlowId
/// </summary>
public interface INodeInstanceRepository : IRepository<NodeInstance>
{
    Task UpdateAsync(NodeInstance entity, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NodeInstance>> GetByProcessIdAsync(
        Guid processId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NodeInstance>> GetByTokenIdAsync(
        Guid tokenId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Idempotency helper:
    /// Returns an "open" node instance for the same correlation (if exists), otherwise null.
    /// Open means: Created/Processing/Waiting.
    /// </summary>
    Task<NodeInstance?> TryFindOpenAsync(
        Guid processId,
        Guid tokenId,
        string elementId,
        Guid? scopeId,
        Guid? activityInstanceId,
        IEnumerable<string>? arrivedViaFlowIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the latest node instance in a process (by CreatedAtUtc).
    /// </summary>
    Task<NodeInstance?> GetLastAsync(
        Guid processId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// True if any node instance exists for (processId, elementId).
    /// Useful for simple "has this element been visited" queries.
    /// </summary>
    Task<bool> ExistsForElementAsync(
        Guid processId,
        string elementId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<NodeInstance>> GetByActivityInstanceIdAsync(Guid processId, Guid activityInstanceId, CancellationToken trxCt);
}