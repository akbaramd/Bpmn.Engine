using MediatR;

namespace Novin.Bpmn.Engine.Domain.Common;

/// <summary>
/// Marker interface for domain events
/// </summary>
public interface IDomainEvent : INotification
{
}

