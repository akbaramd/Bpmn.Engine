using Novin.Bpmn.Dashbaord.Controllers;
using Novin.Bpmn.Dashbaord.Data;
using Novin.Bpmn.V3;
using System;

namespace Novin.Bpmn.Dashbaord.Services
{
    public class BpmnV3EngineFactory : IBpmnV3EngineFactory
    {
        private readonly ApplicationDbContext _context;

        public BpmnV3EngineFactory(ApplicationDbContext context)
        {
            _context = context;
        }

        public BpmnV3ProcessExecutor CreateExecutor(string deploymentKey)
        {
            var definition = _context.Definitions.FirstOrDefault(d => d.DefinationKey == deploymentKey);
            if (definition == null)
            {
                throw new ArgumentException($"Definition with key '{deploymentKey}' not found");
            }

            var processInstance = new BpmnV3ProcessInstance("process", definition.Content);
            return new BpmnV3ProcessExecutor(processInstance);
        }

        public BpmnV3ProcessExecutor CreateExecutorFromInstance(BpmnV3ProcessInstance instance)
        {
            return new BpmnV3ProcessExecutor(instance);
        }
    }
} 