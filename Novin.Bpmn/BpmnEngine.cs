using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Core;
using Novin.Bpmn.Executors.Abstracts;
using Novin.Bpmn.Handlers;
using Novin.Bpmn.Models;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Novin.Bpmn
{
    public class BpmnEngine
    {
        private readonly IBpmnTaskAccessor _taskAccessor;
        private readonly IBpmnDefinitionAccessor _definitionAccessor;
        private readonly IBpmnProcessAccessor _processAccessor;
        private readonly IServiceProvider _serviceProvider;

        public BpmnEngine(
            IBpmnTaskAccessor taskAccessor,
            IBpmnDefinitionAccessor definitionAccessor,
            IBpmnProcessAccessor processAccessor,
            IServiceProvider serviceProvider)
        {
            _taskAccessor = taskAccessor ?? throw new ArgumentNullException(nameof(taskAccessor));
            _definitionAccessor = definitionAccessor ?? throw new ArgumentNullException(nameof(definitionAccessor));
            _processAccessor = processAccessor ?? throw new ArgumentNullException(nameof(processAccessor));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public void DeployProcessDefinition(string filePath, string deploymentKey)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException($"Process definition file not found: {filePath}");

            if (string.IsNullOrWhiteSpace(deploymentKey))
                throw new ArgumentException("Deployment key cannot be null or empty", nameof(deploymentKey));

            var definitionXml = File.ReadAllText(filePath);
            _definitionAccessor.StoreDefinition(definitionXml, deploymentKey);
            Console.WriteLine($"Successfully deployed process definition with key: {deploymentKey}");
        }

        public async Task<BpmnV2ProcessExecutor> CreateProcessExecutorAsync(string deploymentKey, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(deploymentKey))
                throw new ArgumentException("Deployment key cannot be null or empty", nameof(deploymentKey));

            var processDefinition = _definitionAccessor.GetDefinitionByDeploymentKey(deploymentKey);
            if (processDefinition == null)
                throw new KeyNotFoundException($"Process definition not found for key: {deploymentKey}");

            var executor = _serviceProvider.GetService(typeof(BpmnV2ProcessExecutor)) as BpmnV2ProcessExecutor;
            if (executor == null)
                throw new InvalidOperationException("Failed to resolve BpmnProcessExecutor from service provider.");

            executor.Initialize(processDefinition.DeploymentKey, processDefinition.Content);
                
            return executor;
        }

        public async Task<BpmnV2ProcessExecutor> CreateProcessExecutorAsync(Guid processId, CancellationToken cancellationToken = default)
        {
            if (processId == Guid.Empty)
                throw new ArgumentException("Process ID cannot be empty", nameof(processId));

            var processState = _processAccessor.GetProcessState(processId);
            if (processState == null)
                throw new KeyNotFoundException($"Process state not found for ID: {processId}");

            var executor = _serviceProvider.GetService(typeof(BpmnV2ProcessExecutor)) as BpmnV2ProcessExecutor;
            if (executor == null)
                throw new InvalidOperationException("Failed to resolve BpmnProcessExecutor from service provider.");

            executor.Initialize(processState);

            return executor;
        }

        public async Task<BpmnV2ProcessExecutor> CompleteUserTaskAsync(Guid taskId, dynamic? variables = null, CancellationToken cancellationToken = default)
        {
            if (taskId == Guid.Empty)
                throw new ArgumentException("Task ID cannot be empty", nameof(taskId));

            var task = await _taskAccessor.RetrieveTask(taskId).ConfigureAwait(false);
            if (task == null)
                throw new KeyNotFoundException($"Task not found for ID: {taskId}");

            var processState = _processAccessor.GetProcessState(task.ProcessId);
            if (processState == null)
                throw new KeyNotFoundException($"Process state not found for ID: {task.ProcessId}");

            var executor = _serviceProvider.GetService(typeof(BpmnV2ProcessExecutor)) as BpmnV2ProcessExecutor;
            if (executor == null)
                throw new InvalidOperationException("Failed to resolve BpmnProcessExecutor from service provider.");

            executor.Initialize(processState);

            if (variables != null)
                executor.Instance.Variables = variables;

            // await executor.CompleteUserTask(taskId).ConfigureAwait(false);
             await executor.StartProcessAsync();
             return executor;
        }

        public async Task<BpmnProcessInstance> GetProcessInstanceAsync(Guid instanceId)
        {
            var processState = _processAccessor.GetProcessState(instanceId);
            if (processState == null)
                throw new KeyNotFoundException($"Process instance not found for ID: {instanceId}");

            return processState;
        }

        public BpmnV2ProcessExecutor GetExecutorForInstance(BpmnProcessInstance instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            var executor = _serviceProvider.GetService(typeof(BpmnV2ProcessExecutor)) as BpmnV2ProcessExecutor;
            if (executor == null)
                throw new InvalidOperationException("Failed to resolve BpmnProcessExecutor from service provider.");

            executor.Initialize(instance);

            return executor;
        }

        public async Task<BpmnProcessInstance> ResumeProcessAsync(Guid processId, CancellationToken cancellationToken = default)
        {
            if (processId == Guid.Empty)
                throw new ArgumentException("Process ID cannot be empty", nameof(processId));

            var processState = _processAccessor.GetProcessState(processId);
            if (processState == null)
                throw new KeyNotFoundException($"Process state not found for ID: {processId}");

            Console.WriteLine($"Resuming process execution for ID: {processId}");

            var executor = _serviceProvider.GetService(typeof(BpmnV2ProcessExecutor)) as BpmnV2ProcessExecutor;
            if (executor == null)
                throw new InvalidOperationException("Failed to resolve BpmnProcessExecutor from service provider.");

            executor.Initialize(processState);

            return await executor.StartProcessAsync(cancellationToken: cancellationToken);
        }
    }
}
