using Novin.Bpmn.Models;

namespace Novin.Bpmn;

public class BpmnNode
{
    public string ElementId { get; set; }
    public Guid Id { get; set; }
    public bool IsExpired { get; set; } = false;
    public DateTime Timestamp { get; set; }
    public bool IsExecutable => Instances.Any(x => x.isExecutable) ;
    public bool CanBeContinue { get; set; } = true;
    public string? Details { get; set; } // Additional execution details if needed

    // History and tracking properties
    
    public List<BpmnSequenceFlow> IncomingFlows { get; set; } = new List<BpmnSequenceFlow>();
    public List<BpmnSequenceFlow> OutgoingFlows { get; set; } = new List<BpmnSequenceFlow>();

    public Stack<(string sourceElementId,Guid sourceNodeId, bool isExecutable)> Merges { get; set; } = new Stack<(string sourceElementId,Guid sourceNodeId, bool isExecutable)>(); // Merges state
    public Stack<(string sourceElementId,Guid sourceNodeId,Guid targetNodeId, bool isExecutable)> Instances { get; set; } = new Stack<(string sourceElementId,Guid sourceNodeId,Guid targetNodeId, bool isExecutable)>(); // Merges state

  
}

public class BpmnNodeTransition
    {
        public Guid Id { get; set; }
        public string ElementId { get; set; }
        public Guid SourceNodeId { get; set; }
        public Guid TargetNodeId { get; set; }
        public DateTime TransitionTime { get; set; }

        public override string ToString()
        {
            return $"{ElementId} - {Id}" ;
        }
    }

