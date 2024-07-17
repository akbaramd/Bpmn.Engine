using System;
using System.Collections.Generic;
using System.Linq;
using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test
{
    public class BpmnBranch
    {
        public Stack<string> Items { get; set; }
        public Stack<string> History { get; set; }
        public List<BpmnBranch> NextBranches { get; set; }

        public BpmnBranch()
        {
            Items = new Stack<string>();
            History = new Stack<string>();
            NextBranches = new List<BpmnBranch>();
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

    public class BpmnBranchFinder
    {
        public List<BpmnBranch> GetBranch(BpmnDefinitions definitions)
        {
            var processes = GetProcesses(definitions);
            var startProcess = FindStartProcess(processes);
            var branches = new List<BpmnBranch>();
            foreach (var element in startProcess.Items)
            {
                if (element is BpmnStartEvent)
                {
                    var startBranch = new BpmnBranch();
                    TraverseElement(startProcess, element, startBranch, branches);
                    branches.Add(startBranch);
                }
            }
            return branches;
        }

        public IEnumerable<BpmnBranch> GetAllBranches(BpmnBranch branch)
        {
            yield return branch;
            foreach (var nextBranch in branch.NextBranches.SelectMany(GetAllBranches))
            {
                yield return nextBranch;
            }
        }

        public List<BpmnBranch> GetAllDistinctBranches(BpmnDefinitions definitions)
        {
            var branches = GetBranch(definitions);
            return branches.SelectMany(GetAllBranches).Distinct().ToList();
        }

        private static IEnumerable<BpmnProcess> GetProcesses(BpmnDefinitions definitions)
        {
            foreach (var item in definitions.Items)
            {
                if (item is BpmnProcess process)
                {
                    yield return process;
                }
            }
        }

        private static BpmnProcess FindStartProcess(IEnumerable<BpmnProcess> processes)
        {
            foreach (var process in processes)
            {
                foreach (var item in process.Items)
                {
                    if (item is BpmnStartEvent)
                    {
                        return process;
                    }
                }
            }
            throw new KeyNotFoundException("No start event found in the BPMN processes.");
        }

        private void TraverseElement(BpmnProcess process, BpmnFlowElement element, BpmnBranch currentBranch, List<BpmnBranch> branches)
        {
            currentBranch.Items.Push(element.id);

            if (element is BpmnEndEvent || element is BpmnGateway)
            {
                foreach (var nextElement in GetNextElements(process, element))
                {
                    var newBranch = new BpmnBranch();
                    TraverseElement(process, nextElement, newBranch, branches);
                    if (newBranch.Items.Any())
                    {
                        currentBranch.NextBranches.Add(newBranch);
                    }
                }
            }
            else
            {
                foreach (var nextElement in GetNextElements(process, element))
                {
                    TraverseElement(process, nextElement, currentBranch, branches);
                }
            }
        }

        private static IEnumerable<BpmnFlowElement> GetNextElements(BpmnProcess process, BpmnFlowElement element)
        {
            // Use sequence flows to find next elements
            foreach (var item in process.Items)
            {
                if (item is BpmnSequenceFlow sequenceFlow && sequenceFlow.sourceRef == element.id)
                {
                    yield return process.Items.First(x => x.id.Equals(sequenceFlow.targetRef));
                }
            }
        }
    }
}
