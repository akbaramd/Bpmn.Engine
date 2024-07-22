using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Executors.Abstracts;
using Novin.Bpmn.Models;

namespace Novin.Bpmn.Executors
{
    public class ServiceTaskExecutor : IExecutor
    {

        public async Task ExecuteAsync(BpmnNode node, BpmnEngine engine)
        {
            var serviceTask = engine.DefinitionsHandler.GetElementById(node.ElementId) as BpmnServiceTask;
            if (serviceTask != null)
            {

                var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes())
                    .FirstOrDefault(x => x.IsAssignableTo(typeof(IServiceTaskHandler)) && x.Name.Equals(serviceTask.implementation));


                if (types != null)
                {
                    var handler = (IServiceTaskHandler) Activator.CreateInstance(types);

                    await handler?.HandleAsync(engine.State)!;
                }

                Console.WriteLine($"Service task {node.ElementId} executed. Response: {serviceTask.implementation}");
            }
        }
    }
}