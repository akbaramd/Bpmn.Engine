using Novin.Bpmn.EventSourcing.Core.Executions;
using Novin.Bpmn.EventSourcing.Events;
using System;
using System.Threading;
using System.Threading.Tasks;
using ExecutionContext = Novin.Bpmn.EventSourcing.Core.Executions.ExecutionContext;

public class ElementProcessingEventHandler : BpmnEventHandlerBase<ElementProcessing>
{
    private readonly IExecutionContextRepository _contextRepository;

    public ElementProcessingEventHandler(IServiceProvider serviceProvider,
                                         IExecutionContextRepository contextRepository)
        : base(serviceProvider)
    {
        _contextRepository = contextRepository ?? throw new ArgumentNullException(nameof(contextRepository));
    }

    public override async Task HandleAsync(ElementProcessing @event, CancellationToken cancellationToken = default)
    {
        var context = _contextRepository.Get(@event.ExecutionId);
        if (context == null)
            throw new InvalidOperationException($"ExecutionContext not found for Id {@event.ExecutionId}");

        switch (@event)
        {
            case UserTaskProcessing userTask:
                await HandleUserTaskProcessingAsync(userTask, context);
                break;

            case ServiceTaskProcessing serviceTask:
                await HandleServiceTaskProcessingAsync(serviceTask, context);
                break;

            case ScriptTaskProcessing scriptTask:
                await HandleScriptTaskProcessingAsync(scriptTask, context);
                break;

            case BusinessRuleTaskProcessing businessRuleTask:
                await HandleBusinessRuleTaskProcessingAsync(businessRuleTask, context);
                break;

            default:
                await HandleDefaultProcessingAsync(@event, context);
                break;
        }
    }

    private async Task HandleUserTaskProcessingAsync(UserTaskProcessing evt, ExecutionContext context)
    {
        UpdateContextAndPublishCompleted(evt, context);
        await Task.CompletedTask;
    }

    private async Task HandleServiceTaskProcessingAsync(ServiceTaskProcessing evt, ExecutionContext context)
    {
        UpdateContextAndPublishCompleted(evt, context);
        await Task.CompletedTask;
    }

    private async Task HandleScriptTaskProcessingAsync(ScriptTaskProcessing evt, ExecutionContext context)
    {
        UpdateContextAndPublishCompleted(evt, context);
        await Task.CompletedTask;
    }

    private async Task HandleBusinessRuleTaskProcessingAsync(BusinessRuleTaskProcessing evt, ExecutionContext context)
    {
        UpdateContextAndPublishCompleted(evt, context);
        await Task.CompletedTask;
    }

    private async Task HandleDefaultProcessingAsync(ElementProcessing evt, ExecutionContext context)
    {
        UpdateContextAndPublishCompleted(evt, context);
        await Task.CompletedTask;
    }

    private void UpdateContextAndPublishCompleted(ElementProcessing evt, ExecutionContext context)
    {
        context.State = ExecutionState.Active;
        _contextRepository.Save(context);

        var completedEvent = new ElementCompleted
        {
            EventId = Guid.NewGuid(),
            InstanceId = context.InstanceId,
            DeploymentId = evt.DeploymentId,
            DeploymentKey = evt.DeploymentKey,
            ProcessId = evt.ProcessId,
            ElementId = evt.ElementId,
            ExecutionId = context.ContextId,
            Timestamp = DateTime.UtcNow,
            ElementType = evt.ElementType,
            Version = context.Version,
        };

        AppendEvent(completedEvent);
    }
}
