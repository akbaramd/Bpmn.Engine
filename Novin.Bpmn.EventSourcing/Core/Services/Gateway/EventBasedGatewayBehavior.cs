using Novin.Bpmn.EventSourcing.Core.Executions;
using Novin.Bpmn.EventSourcing.Core.Topology;
using ExecutionContext = Novin.Bpmn.EventSourcing.Core.Executions.ExecutionContext;

namespace Novin.Bpmn.EventSourcing.Core.Services.Gateway;

/// <summary>
/// رفتار Event-based Gateway: منتظر event می‌ماند
/// </summary>
public class EventBasedGatewayBehavior : IGatewayBehavior
{
    public string GatewayType => "EventBasedGateway";

    public IReadOnlyList<string> Split(
        ExecutionContext context,
        FlowTopology topology,
        FlowNode gatewayNode,
        IReadOnlyList<string> outgoingTargets,
        IReadOnlyList<SequenceFlow> sequenceFlows)
    {
        // Event-based Gateway: منتظر event می‌ماند
        // این Gateway با condition expression کار نمی‌کند
        // باید منتظر بماند تا یکی از eventهای مربوطه trigger شود
        // فعلاً یک لیست خالی برمی‌گردانیم - باید event handler جداگانه‌ای برای این Gateway باشد
        return Array.Empty<string>();
    }

    public bool CanJoin(
        FlowTopology topology,
        FlowNode gatewayNode,
        IReadOnlyList<string> incomingSequenceFlowIds,
        IReadOnlyList<string> arrivedSequenceFlowIds,
        IReadOnlyList<string>? activeIncomingSequenceFlowIds = null)
    {
        // Event-based Gateway Join: فقط یک token کافی است (مشابه Exclusive)
        return arrivedSequenceFlowIds.Count > 0;
    }
}

