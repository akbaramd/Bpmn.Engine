using Novin.Bpmn.Dashbaord.Data;
using System;
using System.Collections.Generic;
using System.Dynamic;
using ExecutionContext = Novin.Bpmn.EventSourcing.Core.Executions.ExecutionContext;

namespace Novin.Bpmn.Dashbaord.Models
{
    public class ProcessDetailViewModel
    {
        public Guid InstanceId { get; set; }

        public string DeploymentKey { get; set; }

        public string ProcessId { get; set; }

        public string Status { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        public List<ExecutionTrace> Traces { get; set; } = new();

        public List<ExecutionContext> ExecutionContexts { get; set; } = new();

        public Dictionary<Guid, string> CurrentElementByContextId { get; set; } = new();

        public dynamic Variables { get; set; } = new ExpandoObject();
    }

    public class EventNodeInfo
    {
        public string EventId { get; set; }
        public string EventType { get; set; }
        public bool IsTriggered { get; set; }
        public DateTime? TriggerTime { get; set; }
        public string AttachedToElementId { get; set; }
    }
} 