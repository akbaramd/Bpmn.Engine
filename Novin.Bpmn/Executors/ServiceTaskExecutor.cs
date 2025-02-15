﻿using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Core;
using Novin.Bpmn.Models;

namespace Novin.Bpmn.Executors;

public class ServiceTaskExecutor : IServiceTaskExecutor
{

    public async Task ExecuteAsync(BpmnProcessNode processNode, BpmnProcessExecutor processExecutor)
    {
        var definitionsHandler = new BpmnDefinitionsHandler(processExecutor.Instance.Definition);
        var serviceTask = definitionsHandler.GetElementById(processNode.ElementId) as BpmnServiceTask;
        if (serviceTask != null)
        {

            var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes())
                .FirstOrDefault(x => x.IsAssignableTo(typeof(IServiceTaskHandler)) && x.Name.Equals(serviceTask.implementation));


            if (types != null)
            {
                var handler = (IServiceTaskHandler) Activator.CreateInstance(types);

                await handler?.HandleAsync(processExecutor.Instance)!;
                Console.WriteLine($"Service task {processNode.ElementId} executed. Response: {serviceTask.implementation}");
            }

                
        }
    }
}

public class TestServiceHandler : IServiceTaskHandler
{
    public Task HandleAsync(BpmnProcessInstance? processState)
    {
        processState.Variables.Index = 1;
        Console.WriteLine($"handle {processState.Variables.Index}");
        return Task.CompletedTask;
    }
}