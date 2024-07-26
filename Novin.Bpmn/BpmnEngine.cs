using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Core;
using Novin.Bpmn.Executors;
using Novin.Bpmn.Handlers;

namespace Novin.Bpmn
{
    public class BpmnEngine
    {
        private readonly IBpmnUserAccessor bpmnUserAccessor;
        private readonly IBpmnTaskAccessor bpmnTaskAccessor;
        private readonly IBpmnDefinitionAccessor bpmnDefinitionAccessor;
        private readonly IBpmnProcessAccessor bpmnProcessAccessor;

        public BpmnEngine(
            IBpmnUserAccessor bpmnUserAccessor,
            IBpmnTaskAccessor bpmnTaskAccessor,
            IBpmnDefinitionAccessor bpmnDefinitionAccessor,
            IBpmnProcessAccessor bpmnProcessAccessor)
        {
            this.bpmnUserAccessor = bpmnUserAccessor;
            this.bpmnTaskAccessor = bpmnTaskAccessor;
            this.bpmnDefinitionAccessor = bpmnDefinitionAccessor;
            this.bpmnProcessAccessor = bpmnProcessAccessor;
        }

        public void DeployDefinition(string path, string deploymentKey)
        {
            var definitionXml = File.ReadAllText(path);
            bpmnDefinitionAccessor.StoreDefinition(definitionXml, deploymentKey);
        }

        public async Task<BpmnProcessEngine> CreateProcessAsync(string deploymentKey)
        {
            var novinDefinition = bpmnDefinitionAccessor.GetDefinitionByDeploymentKey(deploymentKey);

            var definitionsHandler = new BpmnDefinitionsHandler(novinDefinition.Content);

            var processEngine = new BpmnProcessEngine(
                novinDefinition.Content,
                deploymentKey,
                bpmnUserAccessor, bpmnTaskAccessor, bpmnProcessAccessor,
                new ScriptTaskExecutor(),
                new UserTaskExecutor(bpmnTaskAccessor, bpmnUserAccessor),
                new ServiceTaskExecutor());

            return processEngine;
        }

        public async Task<BpmnProcessEngine> CreateProcessAsync(Guid processId)
        {
            var processState = bpmnProcessAccessor.GetProcessState(processId);
  

            var processEngine = new BpmnProcessEngine(
                processState,
                bpmnUserAccessor, bpmnTaskAccessor, bpmnProcessAccessor,
                new ScriptTaskExecutor(),
                new UserTaskExecutor(bpmnTaskAccessor, bpmnUserAccessor),
                new ServiceTaskExecutor());

            return processEngine;
        }

        public async Task<BpmnProcessInstance> CompleteTaskAsync(Guid taskId, dynamic? variables = null)
        {
            // Find the task by its ID
            var task = await bpmnTaskAccessor.RetrieveTask(taskId);
            if (task == null)
            {
                throw new Exception($"Task with ID {taskId} not found.");
            }

            // Find the process and deployment definition using the task's process ID and deployment key
            var processState = bpmnProcessAccessor.GetProcessState(task.ProcessId);
            if (processState == null)
            {
                throw new Exception($"Process with ID {task.ProcessId} and deployment key {task.DeploymentKey} not found.");
            }


            // Create a process engine instance
            var processEngine = new BpmnProcessEngine(
                processState,
                bpmnUserAccessor,
                bpmnTaskAccessor, 
                bpmnProcessAccessor,
                new ScriptTaskExecutor(),
                new UserTaskExecutor(bpmnTaskAccessor, bpmnUserAccessor),
                new ServiceTaskExecutor());

            // Complete the task using the process engine
            if (variables != null)
            {

                processEngine.Instance.Variables = variables;
            }

            await processEngine.CompleteUserTask(taskId);
            return await processEngine.StartProcess();
        }
    }
}
