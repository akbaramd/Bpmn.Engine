using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Core;
using Novin.Bpmn.Executors.Abstracts;
using Novin.Bpmn.Handlers;
using Novin.Bpmn.Models;

namespace Novin.Bpmn
{
    public class BpmnEngine
    {
        private readonly IBpmnTaskAccessor _bpmnTaskAccessor;
        private readonly IBpmnDefinitionAccessor _bpmnDefinitionAccessor;
        private readonly IBpmnProcessAccessor _bpmnProcessAccessor;

        private readonly IScriptTaskExecutor _scriptTaskExecutor;
        private readonly IUserTaskExecutor _userTaskExecutor;
        private readonly ITimerHandler _boundaryEventHandler;
        private readonly IServiceTaskExecutor _serviceTaskExecutor;

        public BpmnEngine(
            IBpmnTaskAccessor bpmnTaskAccessor,
            IBpmnDefinitionAccessor bpmnDefinitionAccessor,
            IBpmnProcessAccessor bpmnProcessAccessor,
            IScriptTaskExecutor scriptTaskExecutor,
            IUserTaskExecutor userTaskExecutor,
            ITimerHandler boundaryEventHandler,
            IServiceTaskExecutor serviceTaskExecutor)
        {
            _bpmnTaskAccessor = bpmnTaskAccessor ?? throw new ArgumentNullException(nameof(bpmnTaskAccessor));
            _bpmnDefinitionAccessor = bpmnDefinitionAccessor ?? throw new ArgumentNullException(nameof(bpmnDefinitionAccessor));
            _bpmnProcessAccessor = bpmnProcessAccessor ?? throw new ArgumentNullException(nameof(bpmnProcessAccessor));
            _scriptTaskExecutor = scriptTaskExecutor ?? throw new ArgumentNullException(nameof(scriptTaskExecutor));
            _userTaskExecutor = userTaskExecutor ?? throw new ArgumentNullException(nameof(userTaskExecutor));
            _boundaryEventHandler = boundaryEventHandler;
            _serviceTaskExecutor = serviceTaskExecutor ?? throw new ArgumentNullException(nameof(serviceTaskExecutor));
        }

        public void DeployDefinition(string path, string deploymentKey)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException($"Definition file not found: {path}");

            if (string.IsNullOrWhiteSpace(deploymentKey))
                throw new ArgumentException("Deployment key cannot be null or empty", nameof(deploymentKey));

            var definitionXml = File.ReadAllText(path);
            _bpmnDefinitionAccessor.StoreDefinition(definitionXml, deploymentKey);
            Console.WriteLine($"Deployed definition for key: {deploymentKey}");
        }

        public async Task<BpmnProcessExecutor> CreateProcessAsync(string deploymentKey, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(deploymentKey))
                throw new ArgumentException("Deployment key cannot be null or empty", nameof(deploymentKey));

            var novinDefinition = _bpmnDefinitionAccessor.GetDefinitionByDeploymentKey(deploymentKey);
            if (novinDefinition == null)
                throw new KeyNotFoundException($"Definition not found for deployment key: {deploymentKey}");

            return new BpmnProcessExecutor(
                novinDefinition.Content,
                deploymentKey,
                _bpmnTaskAccessor,
                _bpmnProcessAccessor,
                _scriptTaskExecutor,
                _userTaskExecutor,
                _serviceTaskExecutor,_boundaryEventHandler);
        }

        public async Task<BpmnProcessExecutor> CreateProcessAsync(Guid processId, CancellationToken cancellationToken = default)
        {
            if (processId == Guid.Empty)
                throw new ArgumentException("Process ID cannot be empty", nameof(processId));

            var processState = _bpmnProcessAccessor.GetProcessState(processId);
            if (processState == null)
                throw new KeyNotFoundException($"Process state not found for process ID: {processId}");

            return new BpmnProcessExecutor(
                processState,
                _bpmnTaskAccessor,
                _bpmnProcessAccessor,
                _scriptTaskExecutor,
                _userTaskExecutor,
                _serviceTaskExecutor,_boundaryEventHandler);
        }

        public async Task<BpmnProcessInstance> CompleteTaskAsync(Guid taskId, dynamic? variables = null, CancellationToken cancellationToken = default)
        {
            if (taskId == Guid.Empty)
                throw new ArgumentException("Task ID cannot be empty", nameof(taskId));

            var task = await _bpmnTaskAccessor.RetrieveTask(taskId).ConfigureAwait(false);
            if (task == null)
                throw new KeyNotFoundException($"Task with ID {taskId} not found");

            var processState = _bpmnProcessAccessor.GetProcessState(task.ProcessId);
            if (processState == null)
                throw new KeyNotFoundException($"Process state for ID {task.ProcessId} not found");

            var processEngine = new BpmnProcessExecutor(
                processState,
                _bpmnTaskAccessor,
                _bpmnProcessAccessor,
                _scriptTaskExecutor,
                _userTaskExecutor,
                _serviceTaskExecutor,_boundaryEventHandler);

            if (variables != null)
                processEngine.Instance.Variables = variables;

            await processEngine.CompleteUserTask(taskId).ConfigureAwait(false);
            return await processEngine.StartProcess().ConfigureAwait(false);
        }
        
        
     


        public async Task<BpmnProcessInstance> GetProcessInstanceAsync(Guid instanceId)
        {
   

            var processInstance =  _bpmnProcessAccessor.GetProcessState(instanceId);
            if (processInstance == null)
                throw new KeyNotFoundException($"ProcessInstance not found for ID: {instanceId}");

            return processInstance;
        }

        public BpmnProcessExecutor GetExecutor(BpmnProcessInstance instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            return new BpmnProcessExecutor(
                instance,
                _bpmnTaskAccessor,
                _bpmnProcessAccessor,
                _scriptTaskExecutor,
                _userTaskExecutor,
                _serviceTaskExecutor,
                _boundaryEventHandler);
        }

        // Method to resume process execution from a paused state
        public async Task<BpmnProcessInstance> ResumeProcessAsync(Guid processId, CancellationToken cancellationToken = default)
        {
            if (processId == Guid.Empty)
                throw new ArgumentException("Process ID cannot be empty", nameof(processId));

            var processState = _bpmnProcessAccessor.GetProcessState(processId);
            if (processState == null)
                throw new KeyNotFoundException($"Process state for ID {processId} not found");

            Console.WriteLine($"Resuming process execution for process ID {processId}");

            var processEngine = new BpmnProcessExecutor(
                processState,
                _bpmnTaskAccessor,
                _bpmnProcessAccessor,
                _scriptTaskExecutor,
                _userTaskExecutor,
                _serviceTaskExecutor,
                _boundaryEventHandler);

            return await processEngine.StartProcess().ConfigureAwait(false);
        }
    }
}
