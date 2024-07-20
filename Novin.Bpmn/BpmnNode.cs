using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test;

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
        public bool IsExecutable { get; set; } = true;
        public bool IsExpired { get; set; } = false;
        public DateTime Timestamp { get; set; }
        public string? Details { get; set; } // Additional execution details if needed

        // History and tracking properties
        public List<InstanceTransition> IncomingTransitions { get; set; } = new List<InstanceTransition>();
        public List<InstanceTransition> OutgoingTransitions { get; set; } = new List<InstanceTransition>();
        public Stack<string> Merges { get; set; } = new Stack<string>(); // Merges state

        // Method to add a transition
        public void AddTransition(string sourceToken, string targetToken, DateTime transitionTime, bool isIncoming)
        {
            var transition = new InstanceTransition
            {
                SourceToken = sourceToken,
                TargetToken = targetToken,
                TransitionTime = transitionTime
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
        public string SourceToken { get; set; }
        public string TargetToken { get; set; }
        public DateTime TransitionTime { get; set; }
    }

