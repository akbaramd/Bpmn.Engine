using Novin.Bpmn.EventSourcing.Core.Executions;
using ExecutionContext = Novin.Bpmn.EventSourcing.Core.Executions.ExecutionContext;


public class JoinResolverService : IJoinResolverService
{
    public bool CanJoin(FlowTopology topology, string joinNodeId, IEnumerable<ExecutionContext> executionContexts)
    {
        // 1. بررسی می‌کند که آیا همه شاخه‌های ورودی به Join به حالت Completed رسیده‌اند.

        var incomingBranches = topology.Incoming.TryGetValue(joinNodeId, out var sources)
            ? sources
            : new List<string>();

        foreach (var branchId in incomingBranches)
        {
            var context = executionContexts.FirstOrDefault(c => c.CurrentElementId == branchId);
            if (context == null || context.State != ExecutionState.Completed)
            {
                // حداقل یک شاخه کامل نشده
                return false;
            }
        }

        return true;
    }

    public ExecutionContext MergeContexts(FlowTopology topology, string joinNodeId, IEnumerable<ExecutionContext> executionContexts)
    {
        // 2. ادغام Contextها: نمونه ساده جمع کردن متغیرهای محلی

        var mergedContext = new ExecutionContext
        {
            ContextId = Guid.NewGuid(),
            InstanceId = executionContexts.First().InstanceId,
            CurrentElementId = joinNodeId,
            State = ExecutionState.Active,
            LocalVariables = new Dictionary<string, object?>()
        };

        foreach (var context in executionContexts)
        {
            foreach (var kvp in context.LocalVariables)
            {
                // اگر کلید وجود نداشت یا بخواهی overwrite کنی، اینجا مدیریت کن
                mergedContext.LocalVariables[kvp.Key] = kvp.Value;
            }
        }

        return mergedContext;
    }
}