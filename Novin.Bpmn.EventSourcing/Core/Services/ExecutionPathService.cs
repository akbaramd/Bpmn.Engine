using System.Collections.Generic;
using System.Linq;
using Novin.Bpmn.EventSourcing.Core.Executions;
using Novin.Bpmn.EventSourcing.Core.Process;
using Novin.Bpmn.EventSourcing.Core.Services;

public class ExecutionPathService : IExecutionPathService
{
    private readonly IExecutionContextRepository _contextRepository;
    private readonly IProcessStateStore _processStateStore;
    private readonly IFlowTopologyStore _flowTopologyStore;

    public ExecutionPathService(
        IExecutionContextRepository contextRepository,
        IFlowTopologyStore flowTopologyStore,
        IProcessStateStore processStateStore)
    {
        _contextRepository = contextRepository;
        _processStateStore = processStateStore;
        _flowTopologyStore = flowTopologyStore;
    }

    public ExecutionTraceMap BuildExecutionTraces(Guid instanceId)
    {
        var processState = _processStateStore.Get(instanceId)
                           ?? throw new InvalidOperationException($"ProcessState not found for InstanceId {instanceId}");

        var contexts = _contextRepository.GetByInstanceId(instanceId)
            .OrderBy(c => c.Version)
            .ToList();

        if (!contexts.Any())
            throw new InvalidOperationException("No execution contexts found.");

        var flowTopology = _flowTopologyStore.Get(processState.DeploymentId, processState.ProcessId)
            ?? throw new InvalidOperationException($"FlowTopology not found for InstanceId {processState.InstanceId}");

        // Create execution traces
        var traces = contexts.Select((ctx, index) => new ExecutionTrace
        {
            ExecutionId = ctx.ContextId,
            ParentExecutionId = ctx.ParentContextId?.ToString(),
            Path = ctx.Path.ToList(),
            CurrentElementId = ctx.CurrentElementId,
            State = ctx.State,
            IsExecutable = ctx.IsExecutable,
            SequenceId = index + 1,
            LastUpdated = DateTime.UtcNow
        }).ToList();
        
        // Create a map to track executable elements
        var executableElements = new HashSet<string>();
        var nonExecutableElements = new HashSet<string>();
        
        // Identify executable and non-executable elements
        foreach (var trace in traces)
        {
            if (trace.IsExecutable && trace.CurrentElementId != null)
            {
                executableElements.Add(trace.CurrentElementId);
            }
            else if (trace.CurrentElementId != null)
            {
                nonExecutableElements.Add(trace.CurrentElementId);
            }
            
            // Add path elements to appropriate sets
            foreach (var elementId in trace.Path)
            {
                if (trace.IsExecutable)
                {
                    executableElements.Add(elementId);
                }
                else
                {
                    nonExecutableElements.Add(elementId);
                }
            }
        }
        
        // Remove elements from non-executable if they are also in executable
        nonExecutableElements.ExceptWith(executableElements);
        
        // Extract sequence flows from the flow topology
        var sequenceFlows = new List<SequenceFlowTrace>();
        
        // Process all sequence flows from the topology
        foreach (var flowEntry in flowTopology.SequenceFlows)
        {
            var flow = flowEntry.Value;
            var sourceId = flow.SourceRef;
            var targetId = flow.TargetRef;
            
            // Check if this flow already exists
            if (!sequenceFlows.Any(f => f.FlowId == flow.Id))
            {
                var sourceExecutable = executableElements.Contains(sourceId);
                var targetExecutable = executableElements.Contains(targetId);
                var sourceNonExecutable = nonExecutableElements.Contains(sourceId);
                var targetNonExecutable = nonExecutableElements.Contains(targetId);
                
                // A flow is executable only if both source and target are executable
                var isFlowExecutable = sourceExecutable && targetExecutable;
                
                // If either source or target is non-executable, flow is non-executable
                if (sourceNonExecutable || targetNonExecutable)
                {
                    isFlowExecutable = false;
                }
                
                // Find related execution context
                var relatedExecution = traces
                    .FirstOrDefault(t => t.CurrentElementId == sourceId || t.CurrentElementId == targetId)?
                    .ExecutionId;
                
                var flowTrace = new SequenceFlowTrace
                {
                    FlowId = flow.Id,
                    SourceId = sourceId,
                    TargetId = targetId,
                    IsExecutable = isFlowExecutable,
                    State = isFlowExecutable ? ExecutionState.Active : ExecutionState.DeActive,
                    SequenceId = sequenceFlows.Count + 1,
                    RelatedExecutionId = relatedExecution
                };
                
                sequenceFlows.Add(flowTrace);
            }
        }
        
        // Also process paths from execution traces to catch any flows that might not be in the topology
        foreach (var trace in traces)
        {
            if (trace.Path.Count > 1)
            {
                // Create flow traces for each path segment
                for (int i = 0; i < trace.Path.Count - 1; i++)
                {
                    var sourceId = trace.Path[i];
                    var targetId = trace.Path[i + 1];
                    
                    // Try to find the flow ID from topology
                    string flowId = flowTopology.SequenceFlows.Values
                        .FirstOrDefault(f => f.SourceRef == sourceId && f.TargetRef == targetId)?.Id
                        ?? $"flow_{sourceId}_to_{targetId}";
                    
                    // Check if this flow already exists
                    if (!sequenceFlows.Any(f => f.FlowId == flowId))
                    {
                        var sourceExecutable = executableElements.Contains(sourceId);
                        var targetExecutable = executableElements.Contains(targetId);
                        
                        // A flow is executable only if both source and target are executable
                        var isFlowExecutable = sourceExecutable && targetExecutable;
                        
                        var flowTrace = new SequenceFlowTrace
                        {
                            FlowId = flowId,
                            SourceId = sourceId,
                            TargetId = targetId,
                            IsExecutable = isFlowExecutable,
                            State = isFlowExecutable ? ExecutionState.Active : ExecutionState.DeActive,
                            SequenceId = sequenceFlows.Count + 1,
                            RelatedExecutionId = trace.ExecutionId
                        };
                        
                        sequenceFlows.Add(flowTrace);
                    }
                }
            }
        }
        
        var map = new ExecutionTraceMap
        {
            InstanceId = instanceId,
            Traces = traces,
            SequenceFlows = sequenceFlows
        };

        return map;
    }
}