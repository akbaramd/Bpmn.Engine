using Novin.Bpmn.EventSourcing.Core.Executions;
using Novin.Bpmn.EventSourcing.Core.Topology;
using Novin.Bpmn.EventSourcing.Feel;
using ExecutionContext = Novin.Bpmn.EventSourcing.Core.Executions.ExecutionContext;

namespace Novin.Bpmn.EventSourcing.Core.Services.Gateway;

/// <summary>
/// رفتار Exclusive Gateway (XOR): فقط یک مسیر انتخاب می‌شود
/// </summary>
public class ExclusiveGatewayBehavior : IGatewayBehavior
{
    public string GatewayType => "ExclusiveGateway";

    public IReadOnlyList<string> Split(
        ExecutionContext context,
        FlowTopology topology,
        FlowNode gatewayNode,
        IReadOnlyList<string> outgoingTargets,
        IReadOnlyList<SequenceFlow> sequenceFlows)
    {
        var selectedTargets = new List<string>();

        // بررسی شرط‌ها به ترتیب
        foreach (var targetId in outgoingTargets)
        {
            var sequenceFlow = sequenceFlows.FirstOrDefault(f => f.TargetRef == targetId);
            if (sequenceFlow == null)
                continue;

            // اگر default flow است و هیچ شرطی true نشده، آن را انتخاب کن
            if (sequenceFlow.IsDefault && selectedTargets.Count == 0)
            {
                // default flow را برای بعد نگه دار (اگر هیچ شرطی true نشد)
                continue;
            }

            // بررسی شرط
            bool conditionOk = true;
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
            else if (!sequenceFlow.IsDefault)
            {
                // اگر شرطی ندارد و default هم نیست، skip کن
                continue;
            }

            if (conditionOk)
            {
                selectedTargets.Add(targetId);
                // در Exclusive Gateway فقط یک مسیر انتخاب می‌شود
                break;
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
        // Exclusive Gateway Join: فقط یک token از هر incoming sequence flow کافی است
        // اما در واقعیت، فقط یک incoming sequence flow باید token داشته باشد
        return arrivedSequenceFlowIds.Count > 0;
    }
}

