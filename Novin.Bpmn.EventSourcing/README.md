# BPMN Event Sourcing System

This module implements an event sourcing architecture for the BPMN engine. Event sourcing is a pattern where the state of the system is derived from a sequence of events rather than directly manipulating the state.

## Core Components

### Events
Events are immutable records of things that have happened in the system. The events are defined in the `Novin.Bpmn.EventSourcing.Events` namespace:

- `BpmnEvent`: Base abstract record for all events
- `ProcessEvent`: Events related to process instance lifecycle
- `ElementEvent`: Events related to BPMN elements (activities, gateways, events)
- `JobEvent`: Events related to jobs (e.g., service tasks, timers)

### Event Handlers
Event handlers process specific event types and update the state of the system. They're defined in the `Novin.Bpmn.EventSourcing.Core.EventHandlers` namespace:

- `BaseEventHandler<TEvent>`: Abstract base class for all event handlers
- `ElementActivatedHandler`: Handles element activation
- `ElementCompletedHandler`: Handles element completion
- `ParallelGatewayHandler`: Special handler for parallel gateway join logic

### Event Bus
The event bus distributes events to registered handlers:

- `IEventBus`: Defines methods for publishing events
- `ServiceProviderEventBus`: Implementation using dependency injection to find handlers

### Stream Processor
The stream processor reads events from the event store and updates the state store:

- `BpmnProcessStreamProcessor`: Processes events related to BPMN processes

### State Store
The state store maintains the current state of process instances:

- `IStateStore`: Defines methods for getting and saving state
- `BpmnProcessState`: Represents the current state of a process instance

## Architecture

```
                     ┌───────────────┐
                     │               │
                     │  Event Store  │
                     │               │
                     └───────▲───────┘
                             │
                             │ stores
                             │
┌───────────────┐    ┌───────┴───────┐    ┌───────────────┐
│               │    │               │    │               │
│  EventHandler │◄───┤   Event Bus   │◄───┤    Command    │
│               │    │               │    │   Handlers    │
└───────┬───────┘    └───────────────┘    └───────────────┘
        │
        │ updates
        ▼
┌───────────────┐
│               │
│  State Store  │
│               │
└───────────────┘
```

The flow is as follows:

1. Command handlers translate user actions into events
2. Events are published to the event bus
3. Events are persisted in the event store
4. Event handlers process events and update the state store
5. The state store maintains the current state of the system

## Key Features

- **Strong typing**: All events and handlers are strongly typed
- **Separation of concerns**: Events, handlers, and state are clearly separated
- **Extensibility**: New event types and handlers can be added without modifying existing code
- **Testability**: Each component can be tested in isolation
- **Scalability**: Event sourcing allows for horizontal scaling and resilience

## Usage

To use the event sourcing system, register it in your dependency injection container:

```csharp
services.AddBpmnEventSourcing(options =>
{
    options.StateStorePath = "path/to/state";
    options.EventStorePath = "path/to/events";
});

// Auto-discover handlers in assemblies
services.AddBpmnEventHandlers(
    typeof(Program).Assembly, 
    typeof(BpmnEvent).Assembly);
```

Then, inject the `IEventBus` or other components as needed:

```csharp
public class BpmnProcessManager
{
    private readonly IEventBus _eventBus;
    
    public BpmnProcessManager(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }
    
    public async Task StartProcessAsync(string processDefinitionId, Dictionary<string, object> variables)
    {
        var processInstanceId = Guid.NewGuid().ToString();
        
        // Publish events
        await _eventBus.PublishAsync(new ProcessInstanceCreating
        {
            ProcessInstanceId = processInstanceId,
            ProcessDefinitionId = processDefinitionId,
            DeploymentKey = "key",
            DefinitionXml = "<bpmn>...</bpmn>",
            InitialVariables = variables
        });
        
        // More events...
    }
}
```

## Implementation Notes

This event sourcing implementation is inspired by the architecture used in modern workflow engines like Camunda 8 (Zeebe), which use a similar approach to track and execute BPMN processes. 