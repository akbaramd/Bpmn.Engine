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

    public void StoreProcessState(string processId, BpmnProcessState? processState)
    {
        context.Processes.Add(new Process()
        {
Content = JsonConvert.SerializeObject(processId),
ProcessId = processId,
        });
        context.SaveChanges();
    }

    public BpmnProcessState? GetProcessState(string processId)
    {
        var item =  context.Processes.First(x => x.ProcessId.Equals(processId));
        return JsonConvert.DeserializeObject<BpmnProcessState>(item.Content);
    }
}