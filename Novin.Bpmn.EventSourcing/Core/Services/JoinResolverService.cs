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
        if (!topology.Incoming.TryGetValue(joinNodeId, out var incomingIds))
            return false;

        // کانتکست‌هایی که متعلق به شاخه‌های ورودی هستند (یعنی آخرین المان مسیرشان در incomingIds هست)
        var relevantContexts = allContexts
            .Where(c => c.Path != null && c.Path.Any())
            .Where(c => incomingIds.Contains(c.Path.Last()))
            .ToList();

        var activeBranches = relevantContexts
            .Select(c => c.Path.Last())
            .Distinct()
            .ToHashSet();

        foreach (var branch in activeBranches)
        {
            var ctx = relevantContexts.FirstOrDefault(c => c.Path.Last() == branch);
            if (ctx == null || ctx.State != ExecutionState.Completed)
                return false;
        }

        return true;
    }

    public ExecutionContext MergeContexts(FlowTopology topology, string joinNodeId, IEnumerable<ExecutionContext> executionContexts)
    {
        var merged = new ExecutionContext
        {
            ContextId        = Guid.NewGuid(),
            InstanceId       = executionContexts.First().InstanceId,
            ParentContextId  = executionContexts.First().ParentContextId,
            CurrentElementId = joinNodeId,
            State            = ExecutionState.Active,
            LocalVariables   = new Dictionary<string, object?>(),
            Version          = 0,
            Path             = new List<string> { joinNodeId } // مسیر جدید با Join node
        };

        foreach (var ctx in executionContexts)
        {
            foreach (var kv in ctx.LocalVariables)
                merged.LocalVariables[kv.Key] = kv.Value;

            if (ctx.Path != null)
            {
                foreach (var p in ctx.Path)
                    if (!merged.Path.Contains(p))
                        merged.Path.Add(p);
            }
        }

        return merged;
    }

}
