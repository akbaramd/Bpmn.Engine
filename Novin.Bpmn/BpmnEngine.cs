using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Core;
using Novin.Bpmn.Executors.Abstracts;
using Novin.Bpmn.Handlers;

namespace Novin.Bpmn
{
    public class BpmnEngine
    {
        private readonly IBpmnTaskAccessor _bpmnTaskAccessor;
        private readonly IBpmnDefinitionAccessor _bpmnDefinitionAccessor;
        private readonly IBpmnProcessAccessor _bpmnProcessAccessor;

        // Injected executors
        private readonly IScriptTaskExecutor _scriptTaskExecutor;
        private readonly IUserTaskExecutor _userTaskExecutor;
        private readonly IServiceTaskExecutor _serviceTaskExecutor;

        public BpmnEngine(
            IBpmnTaskAccessor bpmnTaskAccessor,
            IBpmnDefinitionAccessor bpmnDefinitionAccessor,
            IBpmnProcessAccessor bpmnProcessAccessor,
            IScriptTaskExecutor scriptTaskExecutor, // Load from DI
            IUserTaskExecutor userTaskExecutor,  // Load from DI
            IServiceTaskExecutor serviceTaskExecutor // Load from DI
        )
        {
            _bpmnTaskAccessor = bpmnTaskAccessor;
            _bpmnDefinitionAccessor = bpmnDefinitionAccessor;
            _bpmnProcessAccessor = bpmnProcessAccessor;
            _scriptTaskExecutor = scriptTaskExecutor;
            _userTaskExecutor = userTaskExecutor;
            _serviceTaskExecutor = serviceTaskExecutor;
        }

        public void DeployDefinition(string path, string deploymentKey)
        {
            var definitionXml = File.ReadAllText(path);
            _bpmnDefinitionAccessor.StoreDefinition(definitionXml, deploymentKey);
        }

        public async Task<BpmnProcessEngine> CreateProcessAsync(string deploymentKey)
        {
            var novinDefinition = _bpmnDefinitionAccessor.GetDefinitionByDeploymentKey(deploymentKey);
            if (novinDefinition == null)
            {
                throw new Exception($"Definition not found for deployment key: {deploymentKey}");
            }

            var processEngine = new BpmnProcessEngine(
                novinDefinition.Content,
                deploymentKey,
                _bpmnTaskAccessor,
                _bpmnProcessAccessor,
                _scriptTaskExecutor,
                _userTaskExecutor,
                _serviceTaskExecutor);

            return processEngine;
        }

        public async Task<BpmnProcessEngine> CreateProcessAsync(Guid processId)
        {
            var processState = _bpmnProcessAccessor.GetProcessState(processId);
            if (processState == null)
            {
                throw new Exception($"Process state not found for process ID: {processId}");
            }

            var processEngine = new BpmnProcessEngine(
                processState,
                _bpmnTaskAccessor,
                _bpmnProcessAccessor,
                _scriptTaskExecutor,
                _userTaskExecutor,
                _serviceTaskExecutor);

            return processEngine;
        }

        public async Task<BpmnProcessInstance> CompleteTaskAsync(Guid taskId, Dictionary<string, object>? variables = null)
        {
            var task = await _bpmnTaskAccessor.RetrieveTask(taskId);
            if (task == null)
            {
                throw new Exception($"Task with ID {taskId} not found.");
            }

            var processState = _bpmnProcessAccessor.GetProcessState(task.ProcessId);
            if (processState == null)
            {
                throw new Exception($"Process state for ID {task.ProcessId} not found.");
            }

            var processEngine = new BpmnProcessEngine(
                processState,
                _bpmnTaskAccessor,
                _bpmnProcessAccessor,
                _scriptTaskExecutor,
                _userTaskExecutor,
                _serviceTaskExecutor);

            if (variables != null)
            {
                foreach (var variable in variables)
                {
                    processEngine.Instance.Variables[variable.Key] = variable.Value;
                }
            }

            await processEngine.CompleteUserTask(taskId);
            return await processEngine.StartProcess();
        }
    }
}
