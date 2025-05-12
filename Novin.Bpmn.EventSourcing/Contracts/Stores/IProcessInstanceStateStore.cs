using System.Diagnostics.CodeAnalysis;
using Novin.Bpmn.EventSourcing.Core.Models;

public sealed class InstanceQuery
{
    public string? InstanceId   { get; init; }
    public string? DeploymentId { get; init; }
    public string? Status       { get; init; }
    public string? Pattern      { get; init; }   // supports * wildcard
    public int     Size         { get; init; } = 1000;
}

/// <summary>
/// Wraps a state payload together with the optimistic-locking version
/// number that was stored alongside it.
/// </summary>
/// <typeparam name="TState">The CLR type of the state payload.</typeparam>
public readonly record struct StateWithVersion<TState>(
    [property: MaybeNull]   TState State,
    [property: NotNull]     long   Version)
    : IComparable<StateWithVersion<TState>>, IEquatable<StateWithVersion<TState>>
{
    /// <summary>
    /// Indicates whether this wrapper is “empty” (no state persisted yet).
    /// </summary>
    public bool IsEmpty => Version == 0;

    /// <summary>
    /// Convenience factory for the common “not found” case.
    /// </summary>
    public static StateWithVersion<TState> Empty { get; } = new(default, 0);

    #region Comparison helpers (optional but nice to have)

    /// <inheritdoc/>
    public int CompareTo(StateWithVersion<TState> other) =>
        Version.CompareTo(other.Version);

    /// <inheritdoc/>
    public bool Equals(StateWithVersion<TState> other) =>
        Version == other.Version &&
        EqualityComparer<TState>.Default.Equals(State, other.State);

    #endregion

    /// <summary>
    /// Deconstruct pattern support:
    /// <code>
    /// var (state, version) = await store.GetAsync(id);
    /// </code>
    /// </summary>
    public void Deconstruct(out TState? state, out long version)
    {
        state   = State;
        version = Version;
    }

    public override string ToString() =>
        $"Version {Version} – {(State is null ? "null" : State.ToString())}";
}

/// <summary>Main entry-point for persisting / retrieving process-instance state.</summary>
public interface IProcessInstanceStateStore
{
    /// <summary>Create or update the whole state document (optimistic concurrency optional).</summary>
    Task UpsertAsync(ProcessInstanceState state,
        long? expectedVersion = null,
        CancellationToken ct  = default);

    /// <summary>Retrieve a state; returns <c>null</c> when not found.</summary>
    Task<StateWithVersion<ProcessInstanceState>?> GetAsync(string instanceId,
        CancellationToken ct = default);

    /// <summary>Remove a state. Returns <c>true</c> when something was deleted.</summary>
    Task<bool> DeleteAsync(string instanceId,
        long? expectedVersion = null,
        CancellationToken ct = default);

    /// <summary>Lightweight existence check.</summary>
    Task<bool> ExistsAsync(string instanceId,
        CancellationToken ct = default);

    /// <summary>Run a query – any combination of filters is allowed.</summary>
    Task<IReadOnlyList<ProcessInstanceState>> QueryAsync(InstanceQuery query,
        CancellationToken ct = default);
}