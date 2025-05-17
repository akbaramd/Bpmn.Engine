using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Novin.Bpmn.EventSourcing.Core.EventStore;
using Novin.Bpmn.EventSourcing.Core.Executions;
using Novin.Bpmn.EventSourcing.Core.Process;
using Novin.Bpmn.EventSourcing.Core.Topology;
using Novin.Bpmn.EventSourcing.Events;
using Novin.Bpmn.EventSourcing.Contracts;
using ExecutionContext = Novin.Bpmn.EventSourcing.Core.Executions.ExecutionContext;

namespace Novin.Bpmn.EventSourcing.Core.Process
{
    public class ProcessEngine : IProcessEngine
    {
        private readonly IBpmnDeploymentStore _deploymentStore;
        private readonly IFlowTopologyStore _topologyStore;
        private readonly IEventStore _eventStore;
        private readonly IProcessStateStore _processStateStore;
        private readonly IExecutionContextRepository _executionContextRepository;

        // Cache for event types to speed up deserialization
        private readonly Dictionary<string, Type> _eventTypeCache = new();

        public ProcessEngine(
            IBpmnDeploymentStore deploymentStore,
            IFlowTopologyStore topologyStore,
            IEventStore eventStore,
            IProcessStateStore processStateStore,
            IExecutionContextRepository executionContextRepository)
        {
            _deploymentStore = deploymentStore ?? throw new ArgumentNullException(nameof(deploymentStore));
            _topologyStore = topologyStore ?? throw new ArgumentNullException(nameof(topologyStore));
            _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
            _processStateStore = processStateStore ?? throw new ArgumentNullException(nameof(processStateStore));
            _executionContextRepository = executionContextRepository ?? throw new ArgumentNullException(nameof(executionContextRepository));
        }
public async Task<ProcessState> StartProcessAsync(string deploymentKey, string processId, Dictionary<string, object?>? initializeVariables = null, CancellationToken cancellationToken = default)
{
    var deployment = _deploymentStore.GetLatest(deploymentKey)
        ?? throw new InvalidOperationException($"Deployment with key '{deploymentKey}' not found.");

    var topology = _topologyStore.Get(deployment.DeploymentId, processId)
        ?? throw new InvalidOperationException($"Topology for process '{processId}' not found in deployment '{deployment.DeploymentId}'.");

    var instanceId = Guid.NewGuid();

    var newState = new ProcessState
    {
        InstanceId = instanceId,
        DeploymentKey = deploymentKey,
        DeploymentId = deployment.DeploymentId,
        ProcessId = processId,
        Variables = initializeVariables != null
            ? new Dictionary<string, object?>(initializeVariables)
            : new Dictionary<string,object?>(),
        Status = ProcessStateStatus.Active,
        CreatedAt = DateTime.UtcNow,
        LastUpdatedAt = DateTime.UtcNow,
        Version = 1
    };

    _processStateStore.Save(newState);

    var startedEvent = new ProcessStarted
    {
        EventId = Guid.NewGuid(),
        InstanceId = instanceId,
        DeploymentKey = deploymentKey,
        DeploymentId = deployment.DeploymentId,
        ProcessId = processId,
        InitializeVariables = initializeVariables ?? new Dictionary<string, object?>(),
        Timestamp = DateTime.UtcNow
    };

    _eventStore.Append(startedEvent);

    return newState;
}


public async Task<ProcessState> StartProcessAsync(ProcessState state, CancellationToken cancellationToken = default)
{
    if (state == null)
        throw new ArgumentNullException(nameof(state));

    var topology = _topologyStore.Get(state.DeploymentId, state.ProcessId)
        ?? throw new InvalidOperationException($"Topology for process '{state.ProcessId}' not found in deployment '{state.DeploymentId}'.");

    // **خواندن کانتکست‌های فعال از repository بر اساس instanceId**
    var activeContexts = _executionContextRepository.GetByInstanceId(state.InstanceId)
        .Where(c => c.State != ExecutionState.Completed)
        .ToList();

    if (!activeContexts.Any())
    {
        // اگر کانتکستی نبود، شروع StartEventها
        await StartInitialElementsAsync(topology, state, cancellationToken);
        return state;
    }

    // انتشار ElementCreated برای کانتکست‌های فعال
    foreach (var ctx in activeContexts)
    {
        if (string.IsNullOrEmpty(ctx.CurrentElementId))
            continue;

        if (!topology.Nodes.TryGetValue(ctx.CurrentElementId, out var node))
            throw new InvalidOperationException($"Node {ctx.CurrentElementId} not found in topology.");

        var elementCreatedEvent = new ElementCreated
        {
            EventId = Guid.NewGuid(),
            InstanceId = state.InstanceId,
            DeploymentId = state.DeploymentId,
            DeploymentKey = state.DeploymentKey,
            ProcessId = state.ProcessId,
            ElementId = ctx.CurrentElementId,
            ElementType = node.ElementType,
            ExecutionId = ctx.ContextId,
            Timestamp = DateTime.UtcNow,
            IsExecutable = true,
            Version = ctx.Version,
            SourceElementId = null,
            SequenceFlowId = null
        };

        _eventStore.Append(elementCreatedEvent);

        await Task.Delay(10, cancellationToken);
    }

    // ادامه اجرای فرآیند
    await ContinueExecutionAsync(state, activeContexts, topology, cancellationToken);

    return state;
}


        private async Task ContinueExecutionAsync(ProcessState state, IEnumerable<ExecutionContext> contexts, FlowTopology topology, CancellationToken cancellationToken)
        {
            foreach (var ctx in contexts)
            {
                if (string.IsNullOrEmpty(ctx.CurrentElementId))
                {
                    await StartInitialElementsAsync(topology, state, cancellationToken);
                    continue;
                }

                if (!topology.Nodes.TryGetValue(ctx.CurrentElementId, out var node))
                {
                    continue;
                }

                if (node.ElementType == BpmnElementType.Task.NameWithNamespace ||
                    node.ElementType == BpmnElementType.ScriptTask.NameWithNamespace)
                {
                    AppendEvent(new ElementCompleted
                    {
                        EventId = Guid.NewGuid(),
                        InstanceId = ctx.InstanceId,
                        DeploymentId = state.DeploymentId,
                        DeploymentKey = state.DeploymentKey,
                        ProcessId = state.ProcessId,
                        ElementId = ctx.CurrentElementId,
                        ExecutionId = ctx.ContextId,
                        ElementType = node.ElementType,
                        Timestamp = DateTime.UtcNow,
                        Version = ctx.Version + 1,
                        IsExecutable = true
                    });

                    await Task.Delay(50, cancellationToken);
                }
                else if (node.IsGateway)
                {
                    // TODO: مدیریت Fork و Join
                }
                else if (node.ElementType == BpmnElementType.EndEvent.NameWithNamespace)
                {
                    ctx.State = ExecutionState.Completed;
                    _executionContextRepository.Save(ctx);

                    // TODO: انتشار ProcessCompleted و بروزرسانی ProcessState
                }
                else
                {
                    // دیگر انواع المان‌ها
                }
            }
        }

        private async Task StartInitialElementsAsync(FlowTopology topology, ProcessState state, CancellationToken cancellationToken)
        {
            var startNodes = topology.Nodes.Values.Where(n => n.IsStartEvent).ToList();

            foreach (var startNode in startNodes)
            {
                // **اینجا کانتکست جدید ساخته می‌شود**
                var newContext = new ExecutionContext
                {
                    ContextId = Guid.NewGuid(),
                    InstanceId = state.InstanceId,
                    State = ExecutionState.Active,
                    IsExecutable = true,
                    LocalVariables = new Dictionary<string, object?>()
                };

                _executionContextRepository.Save(newContext);

                var elementCreated = new ElementCreated
                {
                    EventId = Guid.NewGuid(),
                    InstanceId = state.InstanceId,
                    DeploymentId = topology.DeploymentId,
                    DeploymentKey = state.DeploymentKey,
                    ProcessId = topology.ProcessId,
                    ElementId = startNode.ElementId,
                    ElementType = startNode.ElementType,
                    ExecutionId = newContext.ContextId, // شناسه کانتکست ساخته شده
                    Timestamp = DateTime.UtcNow,
                    IsExecutable = true,
                    Version = 1,
                    SourceElementId = null,
                    SequenceFlowId = null
                };

                _eventStore.Append(elementCreated);

                await Task.Delay(10, cancellationToken);
            }
        }

        private IBpmnEvent? DeserializeEvent(EventEntity entity)
        {
            if (string.IsNullOrWhiteSpace(entity.Payload) || string.IsNullOrWhiteSpace(entity.TypeFullName))
                return null;

            if (!_eventTypeCache.TryGetValue(entity.TypeFullName, out var eventType))
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == entity.AssemblyName);

                if (assembly == null)
                    throw new InvalidOperationException($"Assembly '{entity.AssemblyName}' not loaded.");

                eventType = assembly.GetType(entity.TypeFullName);

                if (eventType == null)
                    throw new InvalidOperationException($"Type '{entity.TypeFullName}' not found in assembly '{entity.AssemblyName}'.");

                _eventTypeCache[entity.TypeFullName] = eventType;
            }

            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                DateFormatHandling = DateFormatHandling.IsoDateFormat
            };

            var deserialized = JsonConvert.DeserializeObject(entity.Payload, eventType, settings);

            if (deserialized != null)
            {
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

        private object? ConvertJTokenToObject(JToken token)
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

        private void AppendEvent(BpmnEvent @event)
        {
            _eventStore.Append(@event);
        }
    }
}
