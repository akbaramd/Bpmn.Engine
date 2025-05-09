using Novin.Bpmn.Contracts;
using Novin.Bpmn.Core;

namespace Novin.Bpmn.Dashbaord.Services
{
    public class BpmnEngineFactory
    {
        private readonly IBpmnEngine _engine;

        public BpmnEngineFactory(IBpmnEngine engine)
        {
            _engine = engine;
        }

        public IBpmnEngine GetEngine()
        {
            return _engine;
        }
    }
} 