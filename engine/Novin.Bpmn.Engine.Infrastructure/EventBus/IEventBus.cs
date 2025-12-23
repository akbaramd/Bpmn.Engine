using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Infrastructure.EventBus;

/// <summary>
/// Event bus interface for publishing domain events
/// </summary>
public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IDomainEvent;
    void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IDomainEvent;
}

/// <summary>
/// Event handler interface
/// </summary>
public interface IEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}

