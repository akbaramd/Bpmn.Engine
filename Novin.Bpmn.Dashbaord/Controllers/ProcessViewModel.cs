using Novin.Bpmn.V3;

namespace Novin.Bpmn.Dashbaord.Controllers
{
    public class ProcessViewModel
    {
        public string DefinitionKey { get; set; } = string.Empty;
        public List<BpmnV3ProcessInstance> Processes { get; set; } = new List<BpmnV3ProcessInstance>();
    }
} 