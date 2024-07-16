using Novin.Bpmn.Test.Executors.Abstracts;
using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test.Executors
{
    public class EndEventExecutor : IEndEventExecutor
    {
        public async Task<List<string>?> ExecuteAsync(BpmnFlowElement element, BpmnEngine engine)
        {
            if (element is BpmnEndEvent)
            {
                Console.WriteLine("Process completed.");
                engine.Instance.ActiveRoutes.Clear(); // Clear active nodes as the process is complete
            }

            return null;
        }
    }
}