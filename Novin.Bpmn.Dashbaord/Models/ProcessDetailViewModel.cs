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
        public dynamic Variables { get; set; } = new ExpandoObject();
    }
} 