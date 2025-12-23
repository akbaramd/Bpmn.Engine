using Novin.Bpmn.EventSourcing.Core.Executions;
using Novin.Bpmn.EventSourcing.Core.Topology;
using Novin.Bpmn.EventSourcing.Feel;
using ExecutionContext = Novin.Bpmn.EventSourcing.Core.Executions.ExecutionContext;

namespace Novin.Bpmn.EventSourcing.Core.Services.Gateway;

/// <summary>
/// رفتار Inclusive Gateway (OR): زیرمجموعه‌ای از مسیرها انتخاب می‌شوند
/// </summary>
public class InclusiveGatewayBehavior : IGatewayBehavior
{
    public string GatewayType => "InclusiveGateway";

    public IReadOnlyList<string> Split(
        ExecutionContext context,
        FlowTopology topology,
        FlowNode gatewayNode,
        IReadOnlyList<string> outgoingTargets,
        IReadOnlyList<SequenceFlow> sequenceFlows)
    {
        var selectedTargets = new List<string>();

        // بررسی شرط‌ها - همه مسیرهایی که شرطشان true است انتخاب می‌شوند
        foreach (var targetId in outgoingTargets)
        {
            var sequenceFlow = sequenceFlows.FirstOrDefault(f => f.TargetRef == targetId);
            if (sequenceFlow == null)
                continue;

            // اگر default flow است، برای بعد نگه دار
            if (sequenceFlow.IsDefault)
                continue;

            // بررسی شرط
            bool conditionOk = false;
            if (!string.IsNullOrWhiteSpace(sequenceFlow.ConditionExpression))
            {
                try
                {
                    conditionOk = FeelEngine.Evaluate<bool>(sequenceFlow.ConditionExpression, context.LocalVariables);
                }
                catch
                {
                    conditionOk = false;
                }
            }

            if (conditionOk)
            {
                selectedTargets.Add(targetId);
            }
        }

        // اگر هیچ شرطی true نشد، default flow را انتخاب کن
        if (selectedTargets.Count == 0)
        {
            var defaultFlow = sequenceFlows.FirstOrDefault(f => f.IsDefault);
            if (defaultFlow != null)
            {
                selectedTargets.Add(defaultFlow.TargetRef);
            }
            else if (outgoingTargets.Count > 0)
            {
                // اگر default flow وجود ندارد، اولین flow را انتخاب کن
                selectedTargets.Add(outgoingTargets[0]);
            }
        }

        return selectedTargets;
    }

    public bool CanJoin(
        FlowTopology topology,
        FlowNode gatewayNode,
        IReadOnlyList<string> incomingSequenceFlowIds,
        IReadOnlyList<string> arrivedSequenceFlowIds,
        IReadOnlyList<string>? activeIncomingSequenceFlowIds = null)
    {
        // Inclusive Gateway Join: همه activeIncomingSequenceFlowIds باید token داشته باشند
        if (activeIncomingSequenceFlowIds == null || activeIncomingSequenceFlowIds.Count == 0)
        {
            // اگر activeIncomingSequenceFlowIds مشخص نشده، همه incoming flows را چک کن
            return incomingSequenceFlowIds.All(flowId => arrivedSequenceFlowIds.Contains(flowId));
        }

        return activeIncomingSequenceFlowIds.All(flowId => arrivedSequenceFlowIds.Contains(flowId));
    }
}

