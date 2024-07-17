namespace Novin.Bpmn.Test.Models
{
    public class BpmnNode
    {
        public string ProcessId { get; set; }
        public string Id { get; set; }
        public BpmnFlowElement Element { get; set; }
        public bool Executed { get; set; }
        public List<BpmnSequenceFlow> Outgoing { get; set; }
        public List<BpmnSequenceFlow> Incoming { get; set; }
        public int ForkedBranchCount { get; set; }
        public List<string> ForkedBranches { get; set; } = new List<string>();
    }
}