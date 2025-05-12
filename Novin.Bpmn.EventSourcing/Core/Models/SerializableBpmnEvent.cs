using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Novin.Bpmn.EventSourcing.Contracts;

namespace Novin.Bpmn.EventSourcing.Core.Models
{
    /// <summary>
    /// Serializable representation of an IBpmnEvent with all common properties.
    /// Used for storage and serialization of IBpmnEvent instances.
    /// </summary>
    public class SerializableBpmnEvent : IBpmnEvent
    {
        // IBpmnEvent implementation
        public Guid EventId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string InstanceId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public Guid DeploymentId { get; set; }
        public string DeploymentKey { get; set; } = string.Empty;
        public string CorrelationId { get; set; } = string.Empty;
        
        // Common properties from various event types
        public string ElementId { get; set; } = string.Empty;
        public string ElementType { get; set; } = string.Empty;
        public string SourceElementId { get; set; } = string.Empty;
        public string SequenceFlowId { get; set; } = string.Empty;
        public List<string> OutgoingFlowIds { get; set; } = new List<string>();
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public bool HasErrorBoundaryEvent { get; set; }
        public string ErrorBoundaryEventId { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string ExecutionId { get; set; } = string.Empty;
        public Dictionary<string, object> Variables { get; set; } = new Dictionary<string, object>();
        
        // Additional metadata for easier event processing
        public string Activity { get; set; } = string.Empty;
        public string ActivityType { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        
        // Helper properties for event type identification
        [JsonIgnore]
        public bool IsProcessEvent => EventType?.StartsWith("Process") == true;
        
        [JsonIgnore]
        public bool IsElementEvent => !string.IsNullOrEmpty(ElementId);
        
        [JsonIgnore]
        public bool IsUserTaskEvent => EventType?.Contains("UserTask") == true;
        
        [JsonIgnore]
        public bool IsServiceTaskEvent => EventType?.Contains("ServiceTask") == true;
        
        [JsonIgnore]
        public bool IsGatewayEvent => EventType?.Contains("Gateway") == true;
        
        [JsonIgnore]
        public bool IsSequenceFlowEvent => EventType?.Contains("SequenceFlow") == true;
        
        [JsonIgnore]
        public bool IsErrorEvent => !string.IsNullOrEmpty(ErrorCode) || !string.IsNullOrEmpty(ErrorMessage);
        
        [JsonIgnore]
        public bool IsStartEvent => EventType?.EndsWith("Started") == true || EventType?.Contains("Start") == true;
        
        [JsonIgnore]
        public bool IsCompletionEvent => EventType?.EndsWith("Completed") == true;
        
        [JsonIgnore]
        public bool IsTerminationEvent => EventType?.EndsWith("Terminated") == true;
        
        [JsonIgnore]
        public bool IsFailureEvent => EventType?.EndsWith("Failed") == true;
        
        // Factory method to convert from any IBpmnEvent
        public static SerializableBpmnEvent FromEvent(IBpmnEvent evt)
        {
            if (evt == null) return null;
            
            var result = new SerializableBpmnEvent
            {
                EventId = evt.EventId,
                EventType = evt.EventType,
                InstanceId = evt.InstanceId,
                Timestamp = evt.Timestamp,
                DeploymentId = evt.DeploymentId,
                DeploymentKey = evt.DeploymentKey,
                CorrelationId = evt.CorrelationId
            };
            
            // Use reflection to copy properties that exist on the source
            var sourceType = evt.GetType();
            var targetType = typeof(SerializableBpmnEvent);
            
            foreach (var prop in targetType.GetProperties())
            {
                if (prop.Name is "EventId" or "EventType" or "InstanceId" or "Timestamp" 
                    or "DeploymentId" or "DeploymentKey" or "CorrelationId")
                    continue; // Already copied above
                
                // Skip JsonIgnore properties
                var ignoreAttr = prop.GetCustomAttributes(typeof(JsonIgnoreAttribute), true);
                if (ignoreAttr.Length > 0)
                    continue;
                
                var sourceProp = sourceType.GetProperty(prop.Name);
                if (sourceProp != null && sourceProp.CanRead)
                {
                    try
                    {
                        var value = sourceProp.GetValue(evt);
                        if (value != null)
                        {
                            prop.SetValue(result, value);
                        }
                    }
                    catch 
                    {
                        // Skip properties that throw exceptions
                    }
                }
            }
            
            // Set the category based on event type
            if (result.EventType?.Contains("Process") == true)
                result.Category = "Process";
            else if (result.EventType?.Contains("UserTask") == true)
                result.Category = "UserTask";
            else if (result.EventType?.Contains("ServiceTask") == true)
                result.Category = "ServiceTask";
            else if (result.EventType?.Contains("Task") == true)
                result.Category = "Task";
            else if (result.EventType?.Contains("Gateway") == true)
                result.Category = "Gateway";
            else if (result.EventType?.Contains("SequenceFlow") == true)
                result.Category = "Flow";
            else if (result.EventType?.Contains("Event") == true)
                result.Category = "Event";
            else
                result.Category = "Other";
            
            return result;
        }
    }
} 