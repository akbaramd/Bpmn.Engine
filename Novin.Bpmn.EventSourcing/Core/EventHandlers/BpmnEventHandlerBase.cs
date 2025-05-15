using Microsoft.Extensions.DependencyInjection;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core.EventStore;
using Novin.Bpmn.EventSourcing.Events;

public abstract class BpmnEventHandlerBase<TEvent> : IBpmnEventHandler<TEvent>
    where TEvent : IBpmnEvent
{
    protected readonly IServiceProvider ServiceProvider;
    protected readonly IEventStore EventStore;

    protected BpmnEventHandlerBase(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        EventStore = serviceProvider.GetRequiredService<IEventStore>();
    }

    public abstract Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);

    protected virtual void AppendEvent(BpmnEvent @event) => EventStore.Append(@event);
}