using Novin.Bpmn.Test.Abstractions;
using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test.Branch
{
    public class BpmnBranchFinder : IBpmnBranchFinder
    {
        private readonly BpmnDefinitions _definitions;

        public BpmnBranchFinder(BpmnDefinitions definitions)
        {
            _definitions = definitions;
        }

        public List<BpmnBranch> GetAllDistinctBranches()
        {
            var processes = GetProcesses(_definitions);
            var startProcess = FindStartProcess(processes);
            var branches = new List<BpmnBranch>();
            var visitedElements = new HashSet<string>();

            foreach (var element in startProcess.Items)
            {
                if (element is BpmnStartEvent startEvent)
                {
                    TraverseElement(startProcess, startEvent, branches, visitedElements);
                }
            }

            return branches;
        }

        private static IEnumerable<BpmnProcess> GetProcesses(BpmnDefinitions definitions)
        {
            return definitions.Items.OfType<BpmnProcess>();
        }

        private static BpmnProcess FindStartProcess(IEnumerable<BpmnProcess> processes)
        {
            foreach (var process in processes)
            {
                if (process.Items.OfType<BpmnStartEvent>().Any())
                {
                    return process;
                }
            }

            throw new KeyNotFoundException("No start event found in the BPMN processes.");
        }

        private void TraverseElement(BpmnProcess process, BpmnFlowElement element, List<BpmnBranch> branches,
            HashSet<string> visitedElements)
        {
            var gateway = element is BpmnGateway;
            if (!gateway && visitedElements.Contains(element.id))
            {
                return;
            }

            var currentBranch = new BpmnBranch();
            var currentStack = new Stack<BpmnFlowElement>();
            currentStack.Push(element);

            while (currentStack.Any())
            {
                var currentElement = currentStack.Pop();
                var gateway2 = currentElement is BpmnGateway;
                if (!gateway2 && visitedElements.Contains(currentElement.id))
                {
                    return;
                }

                visitedElements.Add(currentElement.id);
                currentBranch.Items.Push(currentElement.id);

                if (currentElement is BpmnEndEvent || currentElement is BpmnGateway)
                {
                    branches.Add(currentBranch);
                    foreach (var nextElement in GetNextElements(process, currentElement))
                    {
                        TraverseElement(process, nextElement, branches, visitedElements);
                    }

                    return;
                }

                foreach (var nextElement in GetNextElements(process, currentElement))
                {
                    currentStack.Push(nextElement);
                }
            }

            branches.Add(currentBranch);
        }

        private static IEnumerable<BpmnFlowElement> GetNextElements(BpmnProcess process, BpmnFlowElement element)
        {
            return process.Items
                .OfType<BpmnSequenceFlow>()
                .Where(sequenceFlow => sequenceFlow.sourceRef == element.id)
                .Select(sequenceFlow => process.Items.First(x => x.id.Equals(sequenceFlow.targetRef)));
        }

        public BpmnBranch? FindBranchContainingNode(string nodeId)
        {
            return GetAllDistinctBranches().FirstOrDefault(branch => branch.Items.Contains(nodeId));
        }
    }
}