using Novin.Bpmn.Test.Branch;

namespace Novin.Bpmn.Test.Abstractions;

public interface IBpmnBranchFinder
{
    List<BpmnBranch> GetAllDistinctBranches();
    BpmnBranch? FindBranchContainingNode(string nodeId);
}