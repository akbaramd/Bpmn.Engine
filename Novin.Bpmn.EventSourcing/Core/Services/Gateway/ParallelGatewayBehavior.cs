using Novin.Bpmn.EventSourcing.Core.Executions;
using Novin.Bpmn.EventSourcing.Core.Topology;
using ExecutionContext = Novin.Bpmn.EventSourcing.Core.Executions.ExecutionContext;

namespace Novin.Bpmn.EventSourcing.Core.Services.Gateway;

/// <summary>
/// رفتار Parallel Gateway (AND): همه مسیرها بدون شرط انتخاب می‌شوند
/// </summary>
public class ParallelGatewayBehavior : IGatewayBehavior
{
    public string GatewayType => "ParallelGateway";

    public IReadOnlyList<string> Split(
        ExecutionContext context,
        FlowTopology topology,
        FlowNode gatewayNode,
        IReadOnlyList<string> outgoingTargets,
        IReadOnlyList<SequenceFlow> sequenceFlows)
    {
        // Parallel Gateway: همه مسیرها بدون شرط انتخاب می‌شوند
        return outgoingTargets;
    }

    public bool CanJoin(
        FlowTopology topology,
        FlowNode gatewayNode,
        IReadOnlyList<string> incomingSequenceFlowIds,
        IReadOnlyList<string> arrivedSequenceFlowIds,
        IReadOnlyList<string>? activeIncomingSequenceFlowIds = null)
    {
        // Parallel Gateway Join: همه incoming sequence flows باید token داشته باشند
        return incomingSequenceFlowIds.All(flowId => arrivedSequenceFlowIds.Contains(flowId));
    }
}

