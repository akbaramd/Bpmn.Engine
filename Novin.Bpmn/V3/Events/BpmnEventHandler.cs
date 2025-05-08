using Novin.Bpmn.Models;
using System;
using System.Threading.Tasks;

namespace Novin.Bpmn.V3.Events
{
    /// <summary>
    /// Interface for all event handlers in the BPMN engine
    /// </summary>
    public interface IBpmnEventHandler
    {
        /// <summary>
        /// Initialize the event handler
        /// </summary>
        Task Initialize();
        
        /// <summary>
        /// Register an event to be handled
        /// </summary>
        /// <param name="eventDefinition">The event definition</param>
        /// <param name="boundaryEvent">The boundary event containing this event</param>
        /// <param name="token">The token associated with this event</param>
        /// <param name="isInterrupting">Whether this event interrupts the activity</param>
        /// <returns>The registered event</returns>
        Task<IBpmnEvent> RegisterEvent(object eventDefinition, BpmnBoundaryEvent boundaryEvent, BpmnV3Token token, bool isInterrupting);
        
        /// <summary>
        /// Trigger all events of this handler's type
        /// </summary>
        /// <param name="tokenId">Token ID to trigger events for</param>
        /// <returns>Whether any events were triggered</returns>
        Task<bool> TriggerEvents(Guid tokenId);
        
        /// <summary>
        /// Cancel all events of this handler's type
        /// </summary>
        /// <param name="tokenId">Token ID to cancel events for</param>
        Task CancelEvents(Guid tokenId);
    }
    
    /// <summary>
    /// Base interface for all events in the BPMN engine
    /// </summary>
    public interface IBpmnEvent
    {
        /// <summary>
        /// The event definition from the BPMN schema
        /// </summary>
        object EventDefinition { get; }
        
        /// <summary>
        /// The boundary event containing this event
        /// </summary>
        BpmnBoundaryEvent BoundaryEvent { get; }
        
        /// <summary>
        /// The token associated with this event
        /// </summary>
        BpmnV3Token Token { get; }
        
        /// <summary>
        /// Whether this event interrupts the activity
        /// </summary>
        bool IsInterrupting { get; }
        
        /// <summary>
        /// Whether this event has been triggered
        /// </summary>
        bool IsTriggered { get; }
        
        /// <summary>
        /// Initialize the event
        /// </summary>
        Task Initialize();
        
        /// <summary>
        /// Trigger the event
        /// </summary>
        Task Trigger();
        
        /// <summary>
        /// Cancel the event
        /// </summary>
        Task Cancel();
    }
    
    /// <summary>
    /// Base class for all event handlers
    /// </summary>
    public abstract class BpmnEventHandlerBase<TEventDefinition, TEvent> : IBpmnEventHandler
        where TEvent : IBpmnEvent
    {
        protected readonly BpmnV3ProcessInstance ProcessInstance;
        
        protected BpmnEventHandlerBase(BpmnV3ProcessInstance processInstance)
        {
            ProcessInstance = processInstance;
        }
        
        public virtual Task Initialize()
        {
            return Task.CompletedTask;
        }
        
        public async Task<IBpmnEvent> RegisterEvent(object eventDefinition, BpmnBoundaryEvent boundaryEvent, BpmnV3Token token, bool isInterrupting)
        {
            if (!(eventDefinition is TEventDefinition))
                throw new ArgumentException($"Event definition must be of type {typeof(TEventDefinition).Name}");
            
            var typedEvent = await CreateEvent((TEventDefinition)eventDefinition, boundaryEvent, token, isInterrupting);
            await typedEvent.Initialize();
            return typedEvent;
        }
        
        public abstract Task<TEvent> CreateEvent(TEventDefinition eventDefinition, BpmnBoundaryEvent boundaryEvent, BpmnV3Token token, bool isInterrupting);
        
        public abstract Task<bool> TriggerEvents(Guid tokenId);
        
        public abstract Task CancelEvents(Guid tokenId);
    }
} 