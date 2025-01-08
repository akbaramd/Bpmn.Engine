using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Dashbaord.Data;
using Novin.Bpmn.Dashbaord.Models;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Novin.Bpmn.Dashbaord.Accessors
{
    public class EfBpmnProcessAccessor : IBpmnProcessAccessor
    {
        private readonly ApplicationDbContext context;

        public EfBpmnProcessAccessor(ApplicationDbContext context)
        {
            this.context = context;
        }

        public void StoreProcessState(string deploymentKey, Guid processId, BpmnProcessInstance? processState)
        {
            if (processState == null)
                throw new ArgumentNullException(nameof(processState));

            var definition = context.Definitions.FirstOrDefault(x => x.DefinationKey.Equals(deploymentKey));
            if (definition == null)
                throw new InvalidOperationException($"No definition found with the deployment key: {deploymentKey}");

            var existingProcess = context.Processes.FirstOrDefault(x => x.Id.Equals(processId) && x.DefinitionId == definition.Id);

            if (existingProcess == null)
            {
                var newProcess = new Process
                {
                    Content = JsonConvert.SerializeObject(processState.SaveState()),
                    Id = processId,
                    DefinitionId = definition.Id
                };
                context.Processes.Add(newProcess);
            }
            else
            {
                existingProcess.Content = JsonConvert.SerializeObject(processState);
                context.Processes.Update(existingProcess);
            }
            context.SaveChanges();
        }

        public BpmnProcessInstance? GetProcessState( Guid processId)
        {
            var process = context.Processes
                .Include(x => x.Definition)
                .FirstOrDefault(x => x.Id.Equals(processId));

            if (process == null)
                throw new InvalidOperationException($"No process found with the process ID: {processId}");

            return  JsonConvert.DeserializeObject<BpmnProcessInstance>(process.Content);;
        }
    }
}
