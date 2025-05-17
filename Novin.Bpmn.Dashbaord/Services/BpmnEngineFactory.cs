using Novin.Bpmn.EventSourcing.Core.Process;

namespace Novin.Bpmn.Dashbaord.Services
{
    public class BpmnEngineFactory
    {
        private readonly IProcessEngine _engine;

        public BpmnEngineFactory(IProcessEngine engine)
        {
            _engine = engine;
        }

        public IProcessEngine GetEngine()
        {
            return _engine;
        }
    }
} 