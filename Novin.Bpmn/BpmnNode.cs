using Novin.Bpmn.Models;

namespace Novin.Bpmn;

public class BpmnNode
{
    public string Id { get; set; }
    public Guid Uid { get; set; }
    public bool IsExpired { get; set; } = false;
    public DateTime Timestamp { get; set; }
    public bool IsExecutable => Forks.Any(x => x.Item3);
    public bool CanBeContinue { get; set; } = true;
    public string? Details { get; set; } // Additional execution details if needed

    // History and tracking properties
    
    public List<BpmnSequenceFlow> IncommingFlows { get; set; } = new List<BpmnSequenceFlow>();
    public List<BpmnSequenceFlow> OutgoingTargets { get; set; } = new List<BpmnSequenceFlow>();
    public List<InstanceTransition> IncomingTransitions { get; set; } = new List<InstanceTransition>();
    public List<InstanceTransition> OutgoingTransitions { get; set; } = new List<InstanceTransition>();
    public Stack<Tuple<string,Guid, bool>> Merges { get; set; } = new Stack<Tuple<string, Guid,bool>>(); // Merges state
    public Stack<Tuple<string,Guid, bool>> Forks { get; set; } = new Stack<Tuple<string,Guid, bool>>(); // Merges state

    // Method to add a transition
    public void AddTransition(Guid sourceId, Guid targetId, DateTime transitionTime, bool isIncoming)
    {
        var transition = new InstanceTransition
        {
            SourceToken = sourceId,
            TargetToken = targetId,
            TransitionTime = transitionTime,
        };

        if (isIncoming)
        {
            IncomingTransitions.Add(transition);
        }
        else
        {
            OutgoingTransitions.Add(transition);
        }
    }
}

public class InstanceTransition
    {
        public Guid SourceToken { get; set; }
        public Guid TargetToken { get; set; }
        public DateTime TransitionTime { get; set; }
        
    }

