    namespace Novin.Bpmn;

    public class BpmnNodeState
    {
        public string ElementId { get; set; }
        public bool IsActive { get; set; } = true;
        public int Count { get; set; }
        public bool IsWaiting { get; set; }
    }