using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Dashbaord.Data;
using Novin.Bpmn.Dashbaord.Models;

namespace Novin.Bpmn.Dashbaord;

public class EfProcessAccessor : IProcessAccsessor
{
    private readonly ApplicationDbContext context;

    public EfProcessAccessor(ApplicationDbContext context)
    {
        this.context = context;
    }

    public void StoreProcessState(string deploymentKey, string processId, BpmnProcessState? processState)
    {
        var definition = context.Definitions.First(x => x.DefinationKey.Equals(deploymentKey));
        context.Processes.Add(new Process()
        {
            Content = JsonConvert.SerializeObject(processState),
            ProcessId = processId,
            DefinitionId = definition.Id
        });
        context.SaveChanges();
    }

    public BpmnProcessState? GetProcessState(string deploymentKey, string processId)
    {
        var item = context.Processes.Include(x=>x.Definition).First(x => x.ProcessId.Equals(processId) && x.Definition.DefinationKey.Equals(deploymentKey));
        return JsonConvert.DeserializeObject<BpmnProcessState>(item.Content);
    }
}