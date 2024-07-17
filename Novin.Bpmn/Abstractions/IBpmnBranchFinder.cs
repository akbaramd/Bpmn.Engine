using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test;

public interface IBpmnBranchFinder
{
    List<BpmnBranch> GetAllDistinctBranches();
    BpmnBranch? FindBranchContainingNode(string nodeId);
}