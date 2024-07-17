namespace Novin.Bpmn.Test;

public class BpmnBranch
{
    public string Id { get; set; }
    public Stack<string> Items { get; set; }
    public Stack<string> History { get; set; }

    public BpmnBranch()
    {
        Items = new Stack<string>();
        History = new Stack<string>();
    }

    public override bool Equals(object obj)
    {
        if (obj is BpmnBranch branch)
        {
            return Items.SequenceEqual(branch.Items);
        }

        return false;
    }

    public override int GetHashCode()
    {
        return string.Join(",", Items).GetHashCode();
    }
}