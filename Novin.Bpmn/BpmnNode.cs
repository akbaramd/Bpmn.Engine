using Novin.Bpmn.Models;

namespace Novin.Bpmn;

public class BpmnNode
{
    public string Id { get; set; }
    public Stack<BpmnNodeInstance> Instances { get; set; } = new Stack<BpmnNodeInstance>();
    public List<BpmnSequenceFlow> IncomingFlows { get; set; } = new List<BpmnSequenceFlow>();
    public List<BpmnSequenceFlow> OutgoingFlows { get; set; } = new List<BpmnSequenceFlow>();
}


    public class BpmnNodeInstance
    {
        public HashSet<string> Tokens { get; set; } = new HashSet<string>();
        public bool IsExpired { get; set; } = false;
        public DateTime Timestamp { get; set; }
        public bool IsExecutable => Forks.Any(x => x.Item2);
        public bool CanBeContinue { get; set; } = true;
        public string? Details { get; set; } // Additional execution details if needed

        // History and tracking properties
        public List<BpmnTransition> IncomingTransitions { get; set; } = new List<BpmnTransition>();
        public List<BpmnTransition> OutgoingTransitions { get; set; } = new List<BpmnTransition>();
        public Stack<Tuple<string,bool>> Merges { get; set; } = new Stack<Tuple<string,bool>>(); // Merges state
        public Stack<Tuple<string,bool>> Forks { get; set; } = new Stack<Tuple<string,bool>>(); // Merges state

        // Method to add a transition
        public void AddTransition(string sourceToken, string targetToken, DateTime transitionTime, bool isIncoming,
            string flowSequenceId)
        {
            var transition = new BpmnTransition
            {
                SourceToken = sourceToken,
                TargetToken = targetToken,
                TransitionTime = transitionTime,
                FlowSequenceId = flowSequenceId
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

    public class BpmnTransition
    {
        public string SourceToken { get; set; }
        public string TargetToken { get; set; }
        public DateTime TransitionTime { get; set; }
        
        public string FlowSequenceId { get; set; } // Add FlowSequenceId
    }

