using Novin.Bpmn.Dashbaord.Data;
using Novin.Bpmn.V3;
using System;
using System.Collections.Generic;
using System.Dynamic;

namespace Novin.Bpmn.Dashbaord.Models
{
    public class ProcessDetailViewModel
    {
        public Process Process { get; set; }
        public List<NodeExecutionInfo> ExecutedNodes { get; set; }
        public List<FlowExecutionInfo> ExecutedFlows { get; set; }
        public List<BpmnV3Token> ActiveTokens { get; set; }
        public List<BpmnV3Token> WaitingTokens { get; set; }
        public List<BpmnV3Token> CompletedTokens { get; set; }
        public List<EventNodeInfo> TriggeredEvents { get; set; } = new List<EventNodeInfo>();
        public List<EventNodeInfo> BoundaryEvents { get; set; } = new List<EventNodeInfo>();
        public List<EventNodeInfo> StartEvents { get; set; } = new List<EventNodeInfo>();
        public List<EventNodeInfo> EndEvents { get; set; } = new List<EventNodeInfo>();
        public dynamic Variables { get; set; } = new ExpandoObject();
        
        // Properties needed for ProcessDetail.cshtml view
        public string Status { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
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