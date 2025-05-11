using System.Threading.Tasks;
using Novin.Bpmn.EventSourcing.Core;

namespace Novin.Bpmn.EventSourcing.Contracts;


/// <summary>
/// Generic interface for strongly-typed event handlers
/// </summary>
public interface IEventHandler<TEvent>  where TEvent : IBpmnEvent
{
    Task HandleAsync(TEvent @event, BpmnProcessState state);
} 