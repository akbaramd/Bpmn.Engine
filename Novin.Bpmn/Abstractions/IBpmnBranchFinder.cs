using Novin.Bpmn.Branch;

namespace Novin.Bpmn.Abstractions;

public interface IBpmnBranchFinder
{
    List<BpmnBranch> GetAllDistinctBranches();
    BpmnBranch? FindBranchContainingNode(string nodeId);
}