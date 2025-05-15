using Novin.Bpmn.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.V3.Events
{
    /// <summary>
    /// Adapter class to bridge between the new event system and legacy BaseEvent system
    /// </summary>
    public class LegacyEventAdapter : IBpmnEventHandler
    {
        private readonly BpmnV3ProcessInstance _processInstance;
        private readonly Dictionary<Guid, List<BaseEvent>> _legacyEvents = new Dictionary<Guid, List<BaseEvent>>();
        
        public LegacyEventAdapter(BpmnV3ProcessInstance processInstance)
        {
            _processInstance = processInstance;
        }
        
        public Task Initialize()
        {
            return Task.CompletedTask;
        }
        
        public Task<IBpmnEvent> RegisterEvent(object eventDefinition, BpmnBoundaryEvent boundaryEvent, BpmnV3Token token, bool isInterrupting)
        {
            // Create the appropriate legacy event
            BaseEvent legacyEvent = null;
            
            if (eventDefinition is BpmnErrorEventDefinition errorDef)
            {
                legacyEvent = new Novin.Bpmn.V3.ErrorEvent(boundaryEvent, errorDef, token);
            }
            // Add other event types as needed for backward compatibility
            
            if (legacyEvent != null)
            {
                // Initialize the event
                legacyEvent.Initialize();
                
                // Store it in our local dictionary
                if (!_legacyEvents.ContainsKey(token.Id))
                {
                    _legacyEvents[token.Id] = new List<BaseEvent>();
                }
                _legacyEvents[token.Id].Add(legacyEvent);
                
                // Also add to the process instance's token events for backward compatibility
                if (!_processInstance.TokenEvents.ContainsKey(token.Id))
                {
                    _processInstance.TokenEvents[token.Id] = new List<BaseEvent>();
                }
                _processInstance.TokenEvents[token.Id].Add(legacyEvent);
                
                // Create a wrapper for the new event system
                return Task.FromResult<IBpmnEvent>(new LegacyEventWrapper(legacyEvent, boundaryEvent, token, isInterrupting));
            }
            
            return Task.FromResult<IBpmnEvent>(null);
        }
        
        public async Task<bool> TriggerEvents(Guid tokenId)
        {
            if (_legacyEvents.TryGetValue(tokenId, out var events) && events.Count > 0)
            {
                foreach (var legacyEvent in events)
                {
                    await legacyEvent.Trigger();
                }
                
                return events.Exists(e => e.IsTriggered);
            }
            
            return false;
        }
        
        public Task CancelEvents(Guid tokenId)
        {
            // Legacy events don't have an explicit Cancel method
            if (_legacyEvents.ContainsKey(tokenId))
            {
                _legacyEvents.Remove(tokenId);
            }
            
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Wrapper class to adapt legacy BaseEvent to the new IBpmnEvent interface
        /// </summary>
        private class LegacyEventWrapper : IBpmnEvent
        {
            private readonly BaseEvent _legacyEvent;
            
            public LegacyEventWrapper(BaseEvent legacyEvent, BpmnBoundaryEvent boundaryEvent, BpmnV3Token token, bool isInterrupting)
            {
                _legacyEvent = legacyEvent;
                BoundaryEvent = boundaryEvent;
                Token = token;
                IsInterrupting = isInterrupting;
            }
            
            public object EventDefinition => _legacyEvent.Event;
            public BpmnBoundaryEvent BoundaryEvent { get; }
            public BpmnV3Token Token { get; }
            public bool IsInterrupting { get; }
            public bool IsTriggered => _legacyEvent.IsTriggered;
            
            public Task Initialize()
            {
                _legacyEvent.Initialize();
                return Task.CompletedTask;
            }
            
            public async Task Trigger()
            {
                await _legacyEvent.Trigger();
            }
            
            public Task Cancel()
            {
                // Legacy events don't have an explicit Cancel method
                return Task.CompletedTask;
            }
        }
    }
} 