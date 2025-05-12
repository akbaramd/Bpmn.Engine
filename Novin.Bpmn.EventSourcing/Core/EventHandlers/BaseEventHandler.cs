using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core.Models;

namespace Novin.Bpmn.EventSourcing.Core.EventHandlers
{
    /// <summary>
    /// Base class for all BPMN event handlers.
    /// Automatically loads state, invokes lifecycle hooks, persists state & events, and logs.
    /// </summary>
    /// <typeparam name="TEvent">Type of BPMN event to handle</typeparam>
    public abstract class BaseEventHandler<TEvent> : IBpmnEventHandler<TEvent>
        where TEvent : IBpmnEvent
    {
        protected readonly ILogger<BaseEventHandler<TEvent>> Logger;
        protected readonly IProcessInstanceStateStore StateStore;
        protected readonly IEventStore EventStore;
        protected readonly IProcessDeploymentStore DefinitionStore;

        protected BaseEventHandler(
            IProcessInstanceStateStore stateStore,
            IEventStore eventStore,
            IProcessDeploymentStore definitionStore,
            ILogger<BaseEventHandler<TEvent>> logger)
        {
            StateStore      = stateStore      ?? throw new ArgumentNullException(nameof(stateStore));
            EventStore      = eventStore      ?? throw new ArgumentNullException(nameof(eventStore));
            DefinitionStore = definitionStore ?? throw new ArgumentNullException(nameof(definitionStore));
            Logger          = logger          ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default)
        {
            if (@event is null) throw new ArgumentNullException(nameof(@event));

            // 1. Load and validate process state
            var state = await StateStore
                .GetAsync(@event.ProcessInstanceId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    $"Process instance '{@event.ProcessInstanceId}' not found.");

            Logger.LogInformation(
                "⏳ Handling {EventType} for instance {InstanceId}",
                @event.EventType, @event.ProcessInstanceId);

            try
            {
                // 2. Pre‐processing hook
                await BeforeHandleAsync(@event, state, cancellationToken)
                    .ConfigureAwait(false);

                // 3. Main processing (must mutate state)
                await ProcessEventAsync(@event, state, cancellationToken)
                    .ConfigureAwait(false);

                // 4. Persist updated state & record the event
                await StateStore
                    .SaveAsync(state, cancellationToken)
                    .ConfigureAwait(false);
                    
                await EventStore
                    .AppendEventAsync(@event, cancellationToken)
                    .ConfigureAwait(false);

                // 5. Post‐processing hook
                await AfterHandleAsync(@event, state, cancellationToken)
                    .ConfigureAwait(false);

                Logger.LogInformation(
                    "✅ Successfully handled {EventType} for instance {InstanceId}",
                    @event.EventType, @event.ProcessInstanceId);
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    ex,
                    "❌ Error handling {EventType} for instance {InstanceId}",
                    @event.EventType, @event.ProcessInstanceId);
                throw;
            }
        }

        /// <summary>
        /// Override to run logic before <see cref="ProcessEventAsync"/>.
        /// </summary>
        protected virtual Task BeforeHandleAsync(
            TEvent @event,
            ProcessInstanceState state,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        /// <summary>
        /// Must implement the core event‐driven state mutation logic here.
        /// </summary>
        protected abstract Task ProcessEventAsync(
            TEvent @event,
            ProcessInstanceState state,
            CancellationToken cancellationToken);

        /// <summary>
        /// Override to run logic after state & event are persisted.
        /// </summary>
        protected virtual Task AfterHandleAsync(
            TEvent @event,
            ProcessInstanceState state,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
