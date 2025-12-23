using System;
using System.Collections.Generic;

namespace Novin.Bpmn.EventSourcing.Core.Join;

/// <summary>
/// Store برای JoinState - با پشتیبانی از optimistic concurrency
/// </summary>
public interface IJoinStateStore
{
    /// <summary>
    /// دریافت JoinState بر اساس JoinKey
    /// </summary>
    JoinState? Get(string joinKey);

    /// <summary>
    /// دریافت JoinState بر اساس InstanceId و JoinNodeId
    /// </summary>
    JoinState? Get(Guid instanceId, string joinNodeId, int joinCycleId = 0);

    /// <summary>
    /// ذخیره JoinState (با optimistic concurrency check)
    /// </summary>
    /// <returns>true اگر موفق بود، false اگر version conflict داشت</returns>
    bool Save(JoinState joinState);

    /// <summary>
    /// ایجاد JoinState جدید
    /// </summary>
    JoinState Create(
        Guid instanceId,
        string joinNodeId,
        IReadOnlyList<string> requiredIncomingSequenceFlowIds,
        int joinCycleId = 0);

    /// <summary>
    /// حذف JoinState (برای cleanup)
    /// </summary>
    void Remove(string joinKey);

    /// <summary>
    /// دریافت همه JoinStateهای یک Instance
    /// </summary>
    IReadOnlyList<JoinState> GetByInstanceId(Guid instanceId);
}

