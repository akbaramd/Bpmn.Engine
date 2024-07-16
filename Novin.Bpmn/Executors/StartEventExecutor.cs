using Novin.Bpmn.Test.Executors.Abstracts;
using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test.Executors
{
    public class StartEventExecutor : IStartEventExecutor
    {
        public async Task<List<string>?> ExecuteAsync(BpmnFlowElement element, BpmnEngine engine)
        {
            return null;
        }

    }
}