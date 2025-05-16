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

    public ExecutionContext MergeContexts(FlowTopology topology, string joinNodeId,ExecutionContext curernt, IEnumerable<ExecutionContext> executionContexts)
    {

        foreach (var ctx in executionContexts)
        {
            foreach (var kv in ctx.LocalVariables)
                curernt.LocalVariables[kv.Key] = kv.Value;
        }

        return curernt;
    }

}
