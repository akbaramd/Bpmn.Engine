using System;
using System.Collections.Generic;
using System.Linq;

namespace Novin.Bpmn.EventSourcing.Core.Join;

/// <summary>
/// State برای Join Gateway - برای جلوگیری از race condition و idempotency
/// </summary>
public class JoinState
{
    /// <summary>
    /// کلید یکتا برای JoinState: (InstanceId, JoinNodeId, JoinCycleId)
    /// </summary>
    public string JoinKey { get; init; } = default!;

    public Guid InstanceId { get; init; }
    public string JoinNodeId { get; init; } = default!;
    
    /// <summary>
    /// JoinCycleId برای تشخیص joinهای مختلف در loopها
    /// </summary>
    public int JoinCycleId { get; init; } = 0;

    /// <summary>
    /// Tokenهای رسیده: set of (ContextId, SequenceFlowId)
    /// </summary>
    public HashSet<string> ArrivedTokens { get; private set; } = new();

    /// <summary>
    /// آیا join fire شده است؟
    /// </summary>
    public bool Fired { get; private set; } = false;

    /// <summary>
    /// ContextIdهای مصرف شده (برای جلوگیری از استفاده مجدد)
    /// </summary>
    public HashSet<Guid> ConsumedContextIds { get; private set; } = new();

    /// <summary>
    /// Version برای optimistic concurrency
    /// </summary>
    public int Version { get; private set; } = 0;

    /// <summary>
    /// لیست SequenceFlowIdهای incoming که باید token داشته باشند
    /// </summary>
    public IReadOnlyList<string> RequiredIncomingSequenceFlowIds { get; init; } = new List<string>();

    /// <summary>
    /// لیست SequenceFlowIdهای incoming که در split فعال شدند (برای Inclusive Gateway)
    /// </summary>
    public HashSet<string> ActiveIncomingSequenceFlowIds { get; private set; } = new();

    /// <summary>
    /// ثبت arrival یک token
    /// </summary>
    public bool RegisterArrival(Guid contextId, string sequenceFlowId)
    {
        if (Fired)
            return false; // Join قبلاً fire شده

        var tokenKey = $"{contextId}:{sequenceFlowId}";
        if (ArrivedTokens.Contains(tokenKey))
            return false; // این token قبلاً ثبت شده

        ArrivedTokens.Add(tokenKey);
        Version++;
        return true;
    }

    /// <summary>
    /// ثبت ActiveIncomingSequenceFlowIds (برای Inclusive Gateway)
    /// </summary>
    public void SetActiveIncomingSequenceFlowIds(IEnumerable<string> activeFlowIds)
    {
        if (Fired)
            return;

        ActiveIncomingSequenceFlowIds = new HashSet<string>(activeFlowIds);
        Version++;
    }

    /// <summary>
    /// بررسی اینکه آیا می‌توان join را fire کرد
    /// </summary>
    public bool CanFire(IReadOnlyList<string> requiredSequenceFlowIds, bool isInclusiveGateway)
    {
        if (Fired)
            return false;

        if (isInclusiveGateway)
        {
            // برای Inclusive Gateway: همه ActiveIncomingSequenceFlowIds باید token داشته باشند
            var arrivedFlowIds = ArrivedTokens
                .Select(t => t.Split(':')[1])
                .Distinct()
                .ToHashSet();

            return ActiveIncomingSequenceFlowIds.Count > 0 &&
                   ActiveIncomingSequenceFlowIds.All(flowId => arrivedFlowIds.Contains(flowId));
        }
        else
        {
            // برای Parallel/Exclusive Gateway: همه requiredSequenceFlowIds باید token داشته باشند
            var arrivedFlowIds = ArrivedTokens
                .Select(t => t.Split(':')[1])
                .Distinct()
                .ToHashSet();

            return requiredSequenceFlowIds.All(flowId => arrivedFlowIds.Contains(flowId));
        }
    }

    /// <summary>
    /// Fire کردن join و consume کردن tokenها
    /// </summary>
    public IReadOnlyList<Guid> Fire()
    {
        if (Fired)
            return Array.Empty<Guid>();

        Fired = true;
        Version++;

        // استخراج ContextIdهای مصرف شده
        var contextIds = ArrivedTokens
            .Select(t => t.Split(':')[0])
            .Select(Guid.Parse)
            .Distinct()
            .ToList();

        ConsumedContextIds = new HashSet<Guid>(contextIds);
        return contextIds;
    }

    /// <summary>
    /// بررسی اینکه آیا یک context قبلاً consume شده است
    /// </summary>
    public bool IsConsumed(Guid contextId)
    {
        return ConsumedContextIds.Contains(contextId);
    }

    /// <summary>
    /// ساخت JoinKey
    /// </summary>
    public static string CreateJoinKey(Guid instanceId, string joinNodeId, int joinCycleId = 0)
    {
        return $"{instanceId}:{joinNodeId}:{joinCycleId}";
    }

    /// <summary>
    /// Clone کردن JoinState برای جلوگیری از mutation
    /// </summary>
    public JoinState Clone()
    {
        var clone = new JoinState
        {
            JoinKey = JoinKey,
            InstanceId = InstanceId,
            JoinNodeId = JoinNodeId,
            JoinCycleId = JoinCycleId,
            RequiredIncomingSequenceFlowIds = RequiredIncomingSequenceFlowIds
        };

        // Restore mutable state using reflection or internal methods
        // Since we need to set private properties, we'll use a private method
        clone.RestoreState(ArrivedTokens, Fired, ConsumedContextIds, Version, ActiveIncomingSequenceFlowIds);
        
        return clone;
    }

    /// <summary>
    /// Restore state for cloning (internal use)
    /// </summary>
    private void RestoreState(
        HashSet<string> arrivedTokens,
        bool fired,
        HashSet<Guid> consumedContextIds,
        int version,
        HashSet<string> activeIncomingSequenceFlowIds)
    {
        ArrivedTokens = new HashSet<string>(arrivedTokens);
        Fired = fired;
        ConsumedContextIds = new HashSet<Guid>(consumedContextIds);
        Version = version;
        ActiveIncomingSequenceFlowIds = new HashSet<string>(activeIncomingSequenceFlowIds);
    }
}

