using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Core.Process;
using Novin.Bpmn.EventSourcing.Core.Deployments;
using Novin.Bpmn.EventSourcing.Core.Executions;
using Novin.Bpmn.EventSourcing.Core.EventStore;
using Novin.Bpmn.EventSourcing.Events;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using Novin.Bpmn.EventSourcing.Contracts;

namespace Novin.Bpmn.EventSourcing;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            
            // Display menu if no arguments provided
            if (args.Length == 0)
            {
                ShowMenu();
                var choice = Console.ReadLine()?.Trim().ToLower();
                return await RunExample(choice);
            }
            
            return await RunExample(args[0].ToLower());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }
    
    private static void ShowMenu()
    {
        Console.WriteLine("=== BPMN Event Sourcing Engine Examples ===");
        Console.WriteLine("Please select an example:");
        Console.WriteLine("1. Simple Process (Start -> Task -> End)");
        Console.WriteLine("2. Exclusive Gateway Process");
        Console.WriteLine("3. Exclusive Gateway with Default Flow");
        Console.WriteLine("q. Exit");
        Console.Write("Your choice: ");
    }
    
    private static async Task<int> RunExample(string? choice)
    {
        switch (choice)
        {
            case "1":
                return await RunSimpleProcessExample();
            case "2":
                return await RunExclusiveGatewayExample();
            case "3":
                return await RunExclusiveGatewayWithDefaultExample();
            case "q":
            case "quit":
            case "exit":
                return 0;
            default:
                Console.WriteLine("Invalid option. Please try again.");
                return 1;
        }
    }
    
    private static async Task<int> RunSimpleProcessExample()
    {
        Console.WriteLine("\n=== Running Simple Process Example ===");
        
        try
        {
            // Setup services
            var services = new ServiceCollection();
            services.AddBpmnEngine();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            
            var serviceProvider = services.BuildServiceProvider();
            
            // Get required services
            var deploymentService = serviceProvider.GetRequiredService<IDeploymentService>();
            var processEngine = serviceProvider.GetRequiredService<IProcessEngine>();
            var eventStore = serviceProvider.GetRequiredService<IEventStore>();
            var contextRepository = serviceProvider.GetRequiredService<IExecutionContextRepository>();
            var processStateStore = serviceProvider.GetRequiredService<IProcessStateStore>();
            var eventBus = serviceProvider.GetRequiredService<IEventBus>();    
            
            // Read BPMN file
            var bpmnFilePath = Path.Combine("Examples", "SimpleProcess.bpmn");
            if (!File.Exists(bpmnFilePath))
            {
                Console.WriteLine($"Error: BPMN file not found at {bpmnFilePath}");
                return 1;
            }
            
            var bpmnXml = await File.ReadAllTextAsync(bpmnFilePath);
            Console.WriteLine($"✓ Loaded BPMN file: {bpmnFilePath}");
            
            // Deploy
            var deploymentKey = "simple-process-deployment";
            var deployment = deploymentService.Deploy(deploymentKey, bpmnXml);
            Console.WriteLine($"✓ Deployed process. DeploymentId: {deployment.DeploymentId}, Version: {deployment.Version}");
            
            // Start process
            var processId = "SimpleProcess";
            var variables = new Dictionary<string, object?>
            {
                { "message", "Hello from BPMN Engine!" }
            };
            
            Console.WriteLine($"\n🚀 Starting process '{processId}'...");
            var processState = await processEngine.StartProcessAsync(deploymentKey, processId, variables);
            Console.WriteLine($"✓ Process started. InstanceId: {processState.InstanceId}");
            
            // Process events manually (since we're not using HostedService)
            Console.WriteLine("\n📋 Processing events...");
            await ProcessEventsAsync(eventStore, eventBus, serviceProvider, processState.InstanceId);
            
            // Show final state
            Console.WriteLine("\n📊 Final Process State:");
            var finalState = processStateStore.Get(processState.InstanceId);
            if (finalState != null)
            {
                Console.WriteLine($"  Status: {finalState.Status}");
                Console.WriteLine($"  Variables: {string.Join(", ", finalState.Variables.Select(kv => $"{kv.Key}={kv.Value}"))}");
            }
            
            // Show execution contexts
            var contexts = contextRepository.GetByInstanceId(processState.InstanceId);
            Console.WriteLine($"\n📝 Execution Contexts ({contexts.Count}):");
            foreach (var ctx in contexts)
            {
                Console.WriteLine($"  ContextId: {ctx.ContextId}");
                Console.WriteLine($"    State: {ctx.State}");
                Console.WriteLine($"    CurrentElementId: {ctx.CurrentElementId}");
                Console.WriteLine($"    Path: {string.Join(" -> ", ctx.Path)}");
            }
            
            Console.WriteLine("\n✅ Example completed successfully!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }
    
    private static async Task<int> RunExclusiveGatewayExample()
    {
        Console.WriteLine("\n=== Running Exclusive Gateway Process Example ===");
        
        try
        {
            // Setup services
            var services = new ServiceCollection();
            services.AddBpmnEngine();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            
            var serviceProvider = services.BuildServiceProvider();
            
            // Get required services
            var deploymentService = serviceProvider.GetRequiredService<IDeploymentService>();
            var processEngine = serviceProvider.GetRequiredService<IProcessEngine>();
            var eventStore = serviceProvider.GetRequiredService<IEventStore>();
            var contextRepository = serviceProvider.GetRequiredService<IExecutionContextRepository>();
            var processStateStore = serviceProvider.GetRequiredService<IProcessStateStore>();
            var eventBus = serviceProvider.GetRequiredService<IEventBus>();
            
            // Read BPMN file
            var bpmnFilePath = Path.Combine("Examples", "ExclusiveGatewayProcess.bpmn");
            if (!File.Exists(bpmnFilePath))
            {
                Console.WriteLine($"Error: BPMN file not found at {bpmnFilePath}");
                return 1;
            }
            
            var bpmnXml = await File.ReadAllTextAsync(bpmnFilePath);
            Console.WriteLine($"✓ Loaded BPMN file: {bpmnFilePath}");
            
            // Deploy
            var deploymentKey = "exclusive-gateway-deployment";
            var deployment = deploymentService.Deploy(deploymentKey, bpmnXml);
            Console.WriteLine($"✓ Deployed process. DeploymentId: {deployment.DeploymentId}");
            
            // Start process - script task will set decision variable
            var processId = "ExclusiveGatewayProcess";
            var variables = new Dictionary<string, object?>();
            
            Console.WriteLine($"\n🚀 Starting process '{processId}' (script task will set decision variable)...");
            var processState = await processEngine.StartProcessAsync(deploymentKey, processId, variables);
            Console.WriteLine($"✓ Process started. InstanceId: {processState.InstanceId}");
            
            // Process events
            Console.WriteLine("\n📋 Processing events...");
            await ProcessEventsAsync(eventStore, eventBus, serviceProvider, processState.InstanceId);
            
            // Show final state
            Console.WriteLine("\n📊 Final Process State:");
            var finalState = processStateStore.Get(processState.InstanceId);
            if (finalState != null)
            {
                Console.WriteLine($"  Status: {finalState.Status}");
                Console.WriteLine($"  Variables: {string.Join(", ", finalState.Variables.Select(kv => $"{kv.Key}={kv.Value}"))}");
            }
            
            // Show execution contexts
            var contexts = contextRepository.GetByInstanceId(processState.InstanceId);
            Console.WriteLine($"\n📝 Execution Contexts ({contexts.Count}):");
            foreach (var ctx in contexts)
            {
                Console.WriteLine($"  ContextId: {ctx.ContextId}");
                Console.WriteLine($"    State: {ctx.State}");
                Console.WriteLine($"    CurrentElementId: {ctx.CurrentElementId}");
                Console.WriteLine($"    Path: {string.Join(" -> ", ctx.Path)}");
            }
            
            Console.WriteLine("\n✅ Example completed successfully!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }
    
    private static async Task<int> RunExclusiveGatewayWithDefaultExample()
    {
        Console.WriteLine("\n=== Running Exclusive Gateway with Default Flow Example ===");
        
        try
        {
            // Setup services
            var services = new ServiceCollection();
            services.AddBpmnEngine();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            
            var serviceProvider = services.BuildServiceProvider();
            
            // Get required services
            var deploymentService = serviceProvider.GetRequiredService<IDeploymentService>();
            var processEngine = serviceProvider.GetRequiredService<IProcessEngine>();
            var eventStore = serviceProvider.GetRequiredService<IEventStore>();
            var contextRepository = serviceProvider.GetRequiredService<IExecutionContextRepository>();
            var processStateStore = serviceProvider.GetRequiredService<IProcessStateStore>();
            var eventBus = serviceProvider.GetRequiredService<IEventBus>();
            
            // Read BPMN file
            var bpmnFilePath = Path.Combine("Examples", "ExclusiveGatewayWithDefault.bpmn");
            if (!File.Exists(bpmnFilePath))
            {
                Console.WriteLine($"Error: BPMN file not found at {bpmnFilePath}");
                return 1;
            }
            
            var bpmnXml = await File.ReadAllTextAsync(bpmnFilePath);
            Console.WriteLine($"✓ Loaded BPMN file: {bpmnFilePath}");
            
            // Deploy
            var deploymentKey = "exclusive-gateway-default-deployment";
            var deployment = deploymentService.Deploy(deploymentKey, bpmnXml);
            Console.WriteLine($"✓ Deployed process. DeploymentId: {deployment.DeploymentId}");
            
            // Start process with score variable
            var processId = "Process_ExclusiveGateway_Test";
            var variables = new Dictionary<string, object?>
            {
                { "score", 75 } // Score >= 60, should go to Approved path
            };
            
            Console.WriteLine($"\n🚀 Starting process '{processId}' with score={variables["score"]} (should go to Approved path)...");
            var processState = await processEngine.StartProcessAsync(deploymentKey, processId, variables);
            Console.WriteLine($"✓ Process started. InstanceId: {processState.InstanceId}");
            
            // Process events
            Console.WriteLine("\n📋 Processing events...");
            await ProcessEventsAsync(eventStore, eventBus, serviceProvider, processState.InstanceId);
            
            // Show final state
            Console.WriteLine("\n📊 Final Process State:");
            var finalState = processStateStore.Get(processState.InstanceId);
            if (finalState != null)
            {
                Console.WriteLine($"  Status: {finalState.Status}");
                Console.WriteLine($"  Variables: {string.Join(", ", finalState.Variables.Select(kv => $"{kv.Key}={kv.Value}"))}");
            }
            
            // Show execution contexts
            var contexts = contextRepository.GetByInstanceId(processState.InstanceId);
            Console.WriteLine($"\n📝 Execution Contexts ({contexts.Count}):");
            foreach (var ctx in contexts)
            {
                Console.WriteLine($"  ContextId: {ctx.ContextId}");
                Console.WriteLine($"    State: {ctx.State}");
                Console.WriteLine($"    CurrentElementId: {ctx.CurrentElementId}");
                Console.WriteLine($"    Path: {string.Join(" -> ", ctx.Path)}");
            }
            
            Console.WriteLine("\n✅ Example completed successfully!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }
    
    private static async Task ProcessEventsAsync(
        IEventStore eventStore,
        IEventBus eventBus,
        IServiceProvider serviceProvider,
        Guid instanceId,
        int maxIterations = 100)
    {
        for (int i = 0; i < maxIterations; i++)
        {
            var pendingEvents = eventStore.GetIncompletedEvents(50)
                .Where(e => e.InstanceId == instanceId)
                .ToList();
            
            if (pendingEvents.Count == 0)
            {
                // No more events to process
                break;
            }
            
            foreach (var eventEntity in pendingEvents)
            {
                try
                {
                    var bpmnEvent = DeserializeEvent(eventEntity, serviceProvider);
                    if (bpmnEvent != null)
                    {
                        Console.WriteLine($"  📤 Publishing event: {bpmnEvent.EventType} (InstanceId: {bpmnEvent.InstanceId})");
                        await eventBus.PublishAsync(bpmnEvent);
                        eventStore.UpdateStatus(eventEntity.EventId, EventStatus.Sent);
                    }
                    else
                    {
                        Console.WriteLine($"  ⚠️  Could not deserialize event {eventEntity.EventId} of type {eventEntity.TypeFullName}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ❌ Error processing event {eventEntity.EventId}: {ex.Message}");
                    eventStore.UpdateStatus(eventEntity.EventId, EventStatus.Failed, ex.Message);
                }
            }
            
            // Small delay to allow async processing
            await Task.Delay(50);
        }
    }
    
    private static IBpmnEvent? DeserializeEvent(EventEntity eventEntity, IServiceProvider serviceProvider)
    {
        if (string.IsNullOrWhiteSpace(eventEntity.Payload) || string.IsNullOrWhiteSpace(eventEntity.TypeFullName))
            return null;

        try
        {
            // Find the type
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == eventEntity.AssemblyName);

            if (assembly == null)
            {
                Console.WriteLine($"  ⚠️  Assembly '{eventEntity.AssemblyName}' not found for event {eventEntity.EventId}");
                return null;
            }

            var eventType = assembly.GetType(eventEntity.TypeFullName);
            if (eventType == null)
            {
                Console.WriteLine($"  ⚠️  Type '{eventEntity.TypeFullName}' not found in assembly '{eventEntity.AssemblyName}'");
                return null;
            }

            // Deserialize
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                DateFormatHandling = DateFormatHandling.IsoDateFormat
            };

            var deserialized = JsonConvert.DeserializeObject(eventEntity.Payload, eventType, settings);

            if (deserialized != null)
            {
                // Fix Dictionary properties
                var props = eventType.GetProperties();
                foreach (var prop in props)
                {
                    if (prop.PropertyType == typeof(Dictionary<string, object?>))
                    {
                        var val = prop.GetValue(deserialized);
                        if (val is JObject jObj)
                        {
                            var dict = ConvertJTokenToObject(jObj) as Dictionary<string, object?>;
                            prop.SetValue(deserialized, dict);
                        }
                    }
                }
            }

            return deserialized as IBpmnEvent;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠️  Error deserializing event {eventEntity.EventId}: {ex.Message}");
            return null;
        }
    }

    private static object? ConvertJTokenToObject(JToken token)
    {
        return token.Type switch
        {
            JTokenType.Object => token.Children<JProperty>()
                                     .ToDictionary(prop => prop.Name, prop => ConvertJTokenToObject(prop.Value)),
            JTokenType.Array => token.Select(ConvertJTokenToObject).ToList(),
            JTokenType.Integer => token.ToObject<int>(),
            JTokenType.Float => token.ToObject<double>(),
            JTokenType.String => token.ToObject<string>(),
            JTokenType.Boolean => token.ToObject<bool>(),
            JTokenType.Null or JTokenType.Undefined => null,
            _ => token.ToString()
        };
    }
} 