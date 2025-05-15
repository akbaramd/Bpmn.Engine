using Microsoft.Extensions.DependencyInjection;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core.Executions;
using ExecutionContext = Novin.Bpmn.EventSourcing.Core.Executions.ExecutionContext;

namespace Novin.Bpmn.EventSourcing.Core.EventHandlers;

public abstract class ExecutionAwareEventHandlerBase<TEvent> : BpmnEventHandlerBase<TEvent>
    where TEvent : IBpmnEvent
{
    protected readonly IExecutionContextRepository ContextRepository;
    protected readonly IExecutionContextRebuilder ContextRebuilder;

    protected ExecutionAwareEventHandlerBase(IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
        ContextRepository = serviceProvider.GetRequiredService<IExecutionContextRepository>();
        ContextRebuilder = serviceProvider.GetRequiredService<IExecutionContextRebuilder>();
    }

    protected virtual ExecutionContext? GetContext(Guid contextId) =>
        ContextRepository.Get(contextId);

    protected virtual void SaveContext(ExecutionContext context) =>
        ContextRepository.Save(context);

   
}