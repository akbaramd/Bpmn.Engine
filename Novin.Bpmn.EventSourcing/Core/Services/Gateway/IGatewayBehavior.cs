using Novin.Bpmn.EventSourcing.Core.Executions;
using Novin.Bpmn.EventSourcing.Core.Topology;
using Novin.Bpmn.EventSourcing.Events;
using ExecutionContext = Novin.Bpmn.EventSourcing.Core.Executions.ExecutionContext;

namespace Novin.Bpmn.EventSourcing.Core.Services.Gateway;

/// <summary>
/// رفتار Gateway برای Split و Join
/// </summary>
public interface IGatewayBehavior
{
    /// <summary>
    /// نوع Gateway که این behavior برای آن است
    /// </summary>
    string GatewayType { get; }

    /// <summary>
    /// Split: انتخاب مسیر(های) خروجی از Gateway
    /// </summary>
    /// <param name="context">ExecutionContext جاری</param>
    /// <param name="topology">توپولوژی فرآیند</param>
    /// <param name="gatewayNode">Gateway node</param>
    /// <param name="outgoingTargets">لیست targetهای خروجی</param>
    /// <param name="sequenceFlows">SequenceFlowهای مربوط به این Gateway</param>
    /// <returns>لیست targetIdهایی که باید trigger شوند</returns>
    IReadOnlyList<string> Split(
        ExecutionContext context,
        FlowTopology topology,
        FlowNode gatewayNode,
        IReadOnlyList<string> outgoingTargets,
        IReadOnlyList<SequenceFlow> sequenceFlows);

    /// <summary>
    /// Join: بررسی اینکه آیا می‌توان از Join Gateway عبور کرد
    /// </summary>
    /// <param name="topology">توپولوژی فرآیند</param>
    /// <param name="gatewayNode">Gateway node</param>
    /// <param name="incomingSequenceFlowIds">لیست incoming SequenceFlow IDs</param>
    /// <param name="arrivedSequenceFlowIds">لیست SequenceFlow IDs که token رسیده است</param>
    /// <param name="activeIncomingSequenceFlowIds">برای Inclusive Gateway: لیست SequenceFlow IDs که در split فعال شدند</param>
    /// <returns>آیا می‌توان join کرد</returns>
    bool CanJoin(
        FlowTopology topology,
        FlowNode gatewayNode,
        IReadOnlyList<string> incomingSequenceFlowIds,
        IReadOnlyList<string> arrivedSequenceFlowIds,
        IReadOnlyList<string>? activeIncomingSequenceFlowIds = null);
}

