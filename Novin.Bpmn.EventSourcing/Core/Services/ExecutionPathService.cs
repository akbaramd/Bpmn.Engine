using Novin.Bpmn.EventSourcing.Core.Executions;
using Novin.Bpmn.EventSourcing.Core.Process;
using Novin.Bpmn.EventSourcing.Core.Services;

public class ExecutionPathService : IExecutionPathService
{
    private readonly IExecutionContextRepository _contextRepository;
    private readonly IProcessStateStore _processStateStore;

    public ExecutionPathService(
        IExecutionContextRepository contextRepository,
        IProcessStateStore processStateStore)
    {
        _contextRepository = contextRepository;
        _processStateStore = processStateStore;
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

        var map = new ExecutionTraceMap
        {
            InstanceId = instanceId,
            Traces = contexts.Select(ctx => new ExecutionTrace
            {
                ExecutionId = ctx.ContextId,
                ParentExecutionId = ctx.ParentContextId?.ToString(),
                Path = ctx.Path.ToList(),
                CurrentElementId = ctx.CurrentElementId,
                State = ctx.State,
                IsExecutable = ctx.IsExecutable
            }).ToList()
        };

        return map;
    }
}