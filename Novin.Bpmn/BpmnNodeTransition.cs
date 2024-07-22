namespace Novin.Bpmn;

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