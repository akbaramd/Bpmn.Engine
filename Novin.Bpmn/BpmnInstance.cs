using System.Collections.Generic;
using System.Dynamic;

namespace Novin.Bpmn.Test.Models
{
    public class BpmnInstance
    {
        public BpmnDefinitions Definitions { get; }
        public Stack<BpmnNode> History { get; }
        public List<BpmnNode> ActiveRoutes { get; }
        public List<BpmnUserTask> PendingUserTasks { get; }
        public Dictionary<string, List<string>> ParallelGatewayBranches { get; }
        public dynamic Variables { get; } = new ExpandoObject();
        public bool IsPaused { get; set; }
        public BpmnInstance(BpmnDefinitions definitions)
        {
            Definitions = definitions;
            History = new Stack<BpmnNode>();
            ActiveRoutes = new List<BpmnNode>();
            PendingUserTasks = new List<BpmnUserTask>();
            ParallelGatewayBranches = new Dictionary<string, List<string>>();
        }
    }
}