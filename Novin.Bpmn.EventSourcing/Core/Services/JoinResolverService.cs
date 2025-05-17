using Novin.Bpmn.EventSourcing.Core.Executions;
using Novin.Bpmn.EventSourcing.Core.Topology;
using System;
using System.Collections.Generic;
using System.Linq;
using ExecutionContext = Novin.Bpmn.EventSourcing.Core.Executions.ExecutionContext;

public class JoinResolverService : IJoinResolverService
{
    public bool CanJoin(FlowTopology topology, string joinNodeId, IEnumerable<ExecutionContext> allContexts)
    {
        if (!topology.Incoming.TryGetValue(joinNodeId, out var incomingIds) || incomingIds == null || incomingIds.Count == 0)
            return false;

        var res =  (allContexts.Where(x=>x.ReachedToMerge).Select(x=>x.PreviousElementId).Distinct().Count() == incomingIds.Count);
        return res;

    }

    public ExecutionContext MergeContexts(FlowTopology topology, string joinNodeId, ExecutionContext current, IEnumerable<ExecutionContext> executionContexts)
    {
        if (current.LocalVariables == null)
            current.LocalVariables = new Dictionary<string, object?>();

        // ادغام متغیرها با اولویت متغیرهای شاخه‌های ورودی (latest overwrite)
        foreach (var ctx in executionContexts)
        {
            if (ctx.LocalVariables == null)
                continue;

            foreach (var kv in ctx.LocalVariables)
            {
                // اگر کلید وجود داشت بازنویسی کن، در غیر این صورت اضافه کن
                current.LocalVariables[kv.Key] = kv.Value;
            }
        }

        // می‌توان اینجا سایر ادغام‌های احتمالی (مثل مسیر، وضعیت) را هم اضافه کرد

        return current;
    }
}
