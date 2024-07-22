using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Executors.Abstracts;
using Novin.Bpmn.Models;

namespace Novin.Bpmn.Executors
{
    public class ServiceTaskExecutor : IExecutor
    {

        public async Task ExecuteAsync(BpmnProcessNode processNode, BpmnProcessEngine processEngine)
        {
            var serviceTask = processEngine.DefinitionsHandler.GetElementById(processNode.ElementId) as BpmnServiceTask;
            if (serviceTask != null)
            {

                var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes())
                    .FirstOrDefault(x => x.IsAssignableTo(typeof(IServiceTaskHandler)) && x.Name.Equals(serviceTask.implementation));


                if (types != null)
                {
                    var handler = (IServiceTaskHandler) Activator.CreateInstance(types);

                    await handler?.HandleAsync(processEngine.ProcessState)!;
                }

                Console.WriteLine($"Service task {processNode.ElementId} executed. Response: {serviceTask.implementation}");
            }
        }
    }
}