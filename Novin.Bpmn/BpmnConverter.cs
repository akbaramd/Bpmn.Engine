  using Novin.Bpmn.Test;
  using Novin.Bpmn.Test.Models;

  public class BpmnConverter
    {
        // Note : Branch is the sequences of Nodes in Same Path  
        // Note: Branch Can be End in EndEvent Or When reach to node to have multiple path to go (maybe: gateways)
        // Note: End Event Or Other Node Like Gateway (Have more then one Branch) is the LastStep 
        // Note: You can use Dictionary or Stack to handle logic 
        // Note : First Step is StartEvent   
        // Note : Index number is number of index in branch when you find a branch maybe have 4 nodes . index position of node in this branch
        
        public BpmnRoute Convert(BpmnDefinitions definitions)
        {
            var branchFinder = new BpmnBranchFinder();
            var branches = branchFinder.GetAllBranches(definitions).ToList();

            var stepsDictionary = new Dictionary<string, BpmnRoute>();
            var startProcess = FindStartProcess(GetProcesses(definitions));
            var startEvent = startProcess.Items.OfType<BpmnStartEvent>().FirstOrDefault();
            if (startEvent == null)
                throw new KeyNotFoundException("No start event found in the BPMN process.");

            return ConvertElementToStep(startProcess, startEvent, branches, stepsDictionary);
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

        private BpmnRoute ConvertElementToStep(BpmnProcess process, BpmnFlowElement element, List<BpmnBranch> branches, Dictionary<string, BpmnRoute> stepsDictionary)
        {
            if (stepsDictionary.ContainsKey(element.id))
                return stepsDictionary[element.id];

            var branch = FindBranchForElement(element, branches);
            var step = new BpmnRoute
            {
                Id = element.id,
                Element = element,
                Branch = branch,
                NextSteps = new List<BpmnRoute>(),
                Incoming = process.Items.OfType<BpmnSequenceFlow>()
                    .Where(flow => flow.targetRef == element.id).ToList(),
                Outgoing = process.Items.OfType<BpmnSequenceFlow>()
                    .Where(flow => flow.sourceRef == element.id).ToList()
            };

            stepsDictionary[element.id] = step;

            foreach (var nextElement in GetNextElements(process, element))
            {
                var nextStep = ConvertElementToStep(process, nextElement, branches, stepsDictionary);
                step.NextSteps.Add(nextStep);
            }

            return step;
        }

        private static BpmnBranch FindBranchForElement(BpmnFlowElement element, List<BpmnBranch> branches)
        {
            return branches.FirstOrDefault(branch => branch.Items.Contains(element.id));
        }

        private static IEnumerable<BpmnFlowElement> GetNextElements(BpmnProcess process, BpmnFlowElement element)
        {
            foreach (var item in process.Items)
            {
                if (item is BpmnSequenceFlow sequenceFlow && sequenceFlow.sourceRef == element.id)
                {
                    yield return process.Items.First(x=>x.id.Equals(sequenceFlow.targetRef));
                }
            }
        }
    }