using Novin.Bpmn.Abstractions;

namespace Novin.Bpmn.Dashbaord.ServiceHandlers;

public class TestServiceHandler : IServiceTaskHandler
{
    public Task HandleAsync(BpmnProcessInstance? processState)
    {
        processState.Variables.Index = 1;
        Console.WriteLine($"handle {processState.Variables.Index}");
        return Task.CompletedTask;
    }
}