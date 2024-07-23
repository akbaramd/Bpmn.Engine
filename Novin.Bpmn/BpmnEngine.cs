using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Core;
using Novin.Bpmn.Executors;
using Novin.Bpmn.Executors.Abstracts;
using Novin.Bpmn.Handlers;
using Novin.Bpmn.Models;

namespace Novin.Bpmn
{
    public class BpmnEngine
    {
        private readonly IUserAccessor userAccessor;
        private readonly ITaskStorage taskStorage;
        private readonly IDefinitionAccessor definitionAccessor;
        private readonly IProcessAccsessor processAccsessor;

        public BpmnEngine(
            IUserAccessor userAccessor,
            ITaskStorage taskStorage,
            IDefinitionAccessor definitionAccessor,
            IProcessAccsessor processAccsessor)
        {
            this.userAccessor = userAccessor;
            this.taskStorage = taskStorage;
            this.definitionAccessor = definitionAccessor;
            this.processAccsessor = processAccsessor;
        }

        public void DeployDefinition(string path, string deploymentKey, string? version = null)
        {
            var definitionXml = File.ReadAllText(path);
            definitionAccessor.Add(definitionXml, deploymentKey, version);
        }

        public async Task<BpmnProcessEngine> CreateProcessAsync(string deploymentName, string? version = null)
        {
            NovinBpmnDefinitions novinDefinition;
            if (!string.IsNullOrWhiteSpace(version))
            {
                novinDefinition = definitionAccessor.Get(deploymentName, version);
            }
            else
            {
                novinDefinition = definitionAccessor.Get(deploymentName).OrderByDescending(d => d.Version).First();
            }

            var deserializer = new BpmnFileDeserializer();
            var definition = deserializer.Deserialize(novinDefinition.Content);

            var definitionsHandler = new BpmnDefinitionsHandler(definition);
            var state = new BpmnProcessState(definition, definitionsHandler.GetFirstProcess().id);

            var processEngine = new BpmnProcessEngine(
                userAccessor, taskStorage, definitionsHandler, state,
                new ScriptTaskExecutor(),
                new UserTaskExecutor(taskStorage, userAccessor),
                new ServiceTaskExecutor());

            processAccsessor.StoreProcessState(deploymentName,state.Id, state);

            return processEngine;
        }

        public async Task<BpmnProcessEngine> GetProcessEngineAsync(string deploymentName,string processId)
        {
            var processState = processAccsessor.GetProcessState(deploymentName,processId);
            var definitionsHandler = new BpmnDefinitionsHandler(processState.Definition);

            var processEngine = new BpmnProcessEngine(
                userAccessor, taskStorage, definitionsHandler, processState,
                new ScriptTaskExecutor(),
                new UserTaskExecutor(taskStorage, userAccessor),
                new ServiceTaskExecutor());

            return processEngine;
        }
    }
}
