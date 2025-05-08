using Novin.Bpmn.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Novin.Bpmn.V3.Events
{
    public class ErrorEvent : BpmnEventBase<BpmnErrorEventDefinition>
    {
        public string ErrorCode => TypedEventDefinition.errorRef?.Name;
        
        public ErrorEvent(BpmnErrorEventDefinition eventDefinition, BpmnBoundaryEvent boundaryEvent, BpmnV3Token token, 
            bool isInterrupting, BpmnV3ProcessInstance processInstance) 
            : base(eventDefinition, boundaryEvent, token, isInterrupting, processInstance)
        {
        }
        
        public override Task Initialize()
        {
            Console.WriteLine($"Initializing Error Event with code {ErrorCode ?? "N/A"} for {BoundaryEvent?.id ?? "unknown"}");
            return base.Initialize();
        }
    }
    
    public class ErrorEventHandler : BpmnEventHandlerBase<BpmnErrorEventDefinition, ErrorEvent>
    {
        private readonly ConcurrentDictionary<Guid, List<ErrorEvent>> _tokenEvents = new ConcurrentDictionary<Guid, List<ErrorEvent>>();
        
        public ErrorEventHandler(BpmnV3ProcessInstance processInstance) : base(processInstance)
        {
        }
        
        public override async Task<ErrorEvent> CreateEvent(BpmnErrorEventDefinition eventDefinition, BpmnBoundaryEvent boundaryEvent, BpmnV3Token token, bool isInterrupting)
        {
            // Error events are always interrupting unless explicitly set to false
            bool actuallyInterrupting = boundaryEvent.cancelActivity == null || boundaryEvent.cancelActivity;
            
            var errorEvent = new ErrorEvent(eventDefinition, boundaryEvent, token, actuallyInterrupting, ProcessInstance);
            
            // Register the event with the token
            if (!_tokenEvents.TryGetValue(token.Id, out var events))
            {
                events = new List<ErrorEvent>();
                _tokenEvents[token.Id] = events;
            }
            
            events.Add(errorEvent);
            
            return errorEvent;
        }
        
        public override async Task<bool> TriggerEvents(Guid tokenId)
        {
            if (!_tokenEvents.TryGetValue(tokenId, out var events) || !events.Any())
                return false;
            
            Console.WriteLine($"Triggering {events.Count} error events for token {tokenId}");
            
            // Trigger all error events for this token - typically there should be at most one
            foreach (var errorEvent in events)
            {
                await errorEvent.Trigger();
            }
            
            return events.Any(e => e.IsTriggered);
        }
        
        public async Task<bool> TriggerEventsForErrorCode(Guid tokenId, string errorCode)
        {
            if (!_tokenEvents.TryGetValue(tokenId, out var events) || !events.Any())
                return false;
            
            // Find events matching this error code
            var matchingEvents = events.Where(e => 
                string.IsNullOrEmpty(e.ErrorCode) || // Catch-all error handler
                string.IsNullOrEmpty(errorCode) || // Any error
                e.ErrorCode == errorCode // Specific error
            ).ToList();
            
            if (!matchingEvents.Any())
                return false;
            
            Console.WriteLine($"Triggering {matchingEvents.Count} error events for token {tokenId} and error code {errorCode ?? "N/A"}");
            
            // Trigger matching error events
            foreach (var errorEvent in matchingEvents)
            {
                await errorEvent.Trigger();
            }
            
            return matchingEvents.Any(e => e.IsTriggered);
        }
        
        public override async Task CancelEvents(Guid tokenId)
        {
            if (!_tokenEvents.TryGetValue(tokenId, out var events))
                return;
            
            foreach (var errorEvent in events)
            {
                await errorEvent.Cancel();
            }
            
            _tokenEvents.TryRemove(tokenId, out _);
        }
    }
} 