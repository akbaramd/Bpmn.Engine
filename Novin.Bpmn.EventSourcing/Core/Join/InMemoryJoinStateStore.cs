using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Novin.Bpmn.EventSourcing.Core.Join;

/// <summary>
/// In-Memory implementation of IJoinStateStore
/// </summary>
public class InMemoryJoinStateStore : IJoinStateStore
{
    private readonly Dictionary<string, JoinState> _store = new();
    private readonly object _lock = new();

    public JoinState? Get(string joinKey)
    {
        lock (_lock)
        {
            return _store.TryGetValue(joinKey, out var state) ? state.Clone() : null;
        }
    }

    public JoinState? Get(Guid instanceId, string joinNodeId, int joinCycleId = 0)
    {
        var joinKey = JoinState.CreateJoinKey(instanceId, joinNodeId, joinCycleId);
        return Get(joinKey);
    }

    public bool Save(JoinState joinState)
    {
        if (joinState == null)
            throw new ArgumentNullException(nameof(joinState));

        lock (_lock)
        {
            var joinKey = joinState.JoinKey;
            
            if (_store.TryGetValue(joinKey, out var existing))
            {
                // Optimistic concurrency check
                if (existing.Version != joinState.Version)
                {
                    return false; // Version conflict
                }
            }

            // Clone برای جلوگیری از mutation
            // Version is managed internally by JoinState methods
            var cloned = joinState.Clone();
            _store[joinKey] = cloned;
            return true;
        }
    }

    public JoinState Create(
        Guid instanceId,
        string joinNodeId,
        IReadOnlyList<string> requiredIncomingSequenceFlowIds,
        int joinCycleId = 0)
    {
        var joinKey = JoinState.CreateJoinKey(instanceId, joinNodeId, joinCycleId);
        
        lock (_lock)
        {
            if (_store.ContainsKey(joinKey))
            {
                throw new InvalidOperationException($"JoinState with key '{joinKey}' already exists.");
            }

            var joinState = new JoinState
            {
                JoinKey = joinKey,
                InstanceId = instanceId,
                JoinNodeId = joinNodeId,
                JoinCycleId = joinCycleId,
                RequiredIncomingSequenceFlowIds = requiredIncomingSequenceFlowIds
            };

            _store[joinKey] = joinState;
            return joinState;
        }
    }

    public void Remove(string joinKey)
    {
        lock (_lock)
        {
            _store.Remove(joinKey);
        }
    }

    public IReadOnlyList<JoinState> GetByInstanceId(Guid instanceId)
    {
        lock (_lock)
        {
            return _store.Values
                .Where(s => s.InstanceId == instanceId)
                .Select(s => s.Clone())
                .ToList();
        }
    }
}

