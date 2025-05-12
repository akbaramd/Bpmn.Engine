using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Events;
using Novin.Bpmn.EventSourcing.Core.Models;
using Novin.Bpmn.EventSourcing.Events;
using System;

namespace Novin.Bpmn.EventSourcing.Events
{
    // ===========================
    // Task Processing Events
    // ===========================

    /// <summary>
    /// Fired when a UserTask begins its processing phase.
    /// Carries the element and execution identifiers, form key, and optional assignee.
    /// </summary>
    public record UserTaskProcessing : ElementProcessing
    {
        public override string EventType => nameof(UserTaskProcessing);

        public required string FormId { get; init; }

        /// <summary>
        /// The user assigned to this task, if any.
        /// </summary>
        public string? Assignee { get; init; }
    }

    /// <summary>
    /// Fired when a ServiceTask begins its processing phase.
    /// Contains element, execution, and service endpoint details.
    /// </summary>
    public record ServiceTaskProcessing : ElementProcessing
    {
        public override string EventType => nameof(ServiceTaskProcessing);


        /// <summary>
        /// Logical name of the service to invoke.
        /// </summary>
        public required string ServiceName { get; init; }

        /// <summary>
        /// Optional URI endpoint for the service.
        /// </summary>
        public string? Endpoint { get; init; }
    }

    /// <summary>
    /// Fired when a ScriptTask begins its processing phase.
    /// Includes element, execution, script content, and format.
    /// </summary>
    public record ScriptTaskProcessing : ElementProcessing
    {
        public override string EventType => nameof(ScriptTaskProcessing);

        /// <summary>
        /// The actual script code to execute.
        /// </summary>
        public required string Script { get; init; }

        /// <summary>
        /// The scripting language or format (e.g., "groovy", "javascript").
        /// </summary>
        public string? ScriptFormat { get; init; }
    }

    /// <summary>
    /// Fired when a BusinessRuleTask begins its processing phase.
    /// Carries element, execution, and decision key.
    /// </summary>
    public record BusinessRuleTaskProcessing : ElementProcessing
    {
        public override string EventType => nameof(BusinessRuleTaskProcessing);

        /// <summary>
        /// The decision table or rule key to evaluate.
        /// </summary>
        public required string DecisionKey { get; init; }
    }

    /// <summary>
    /// Fired when a ManualTask begins its processing phase.
    /// Includes element, execution, and optional instructions.
    /// </summary>
    public record ManualTaskProcessing : ElementProcessing
    {
        public override string EventType => nameof(ManualTaskProcessing);


        /// <summary>
        /// Optional instructions for manual execution.
        /// </summary>
        public string? Instruction { get; init; }
    }

    /// <summary>
    /// Fired when a ReceiveTask begins its processing phase.
    /// Carries element, execution, and message name.
    /// </summary>
    public record ReceiveTaskProcessing : ElementProcessing
    {
        public override string EventType => nameof(ReceiveTaskProcessing);

        /// <summary>
        /// The message name or signal to listen for.
        /// </summary>
        public required string MessageName { get; init; }
    }

    /// <summary>
    /// Fired when a SendTask begins its processing phase.
    /// Includes element, execution, message details, and optional payload.
    /// </summary>
    public record SendTaskProcessing : ElementProcessing
    {
        public override string EventType => nameof(SendTaskProcessing);

        /// <summary>
        /// The message name or signal to send.
        /// </summary>
        public required string MessageName { get; init; }

        /// <summary>
        /// Optional message payload.
        /// </summary>
        public string? Payload { get; init; }
    }

    /// <summary>
    /// Fired when a generic Task begins its processing phase.
    /// Includes element, execution, and optional implementation hints.
    /// </summary>
    public record TaskProcessing : ElementProcessing
    {
        public override string EventType => nameof(TaskProcessing);


        /// <summary>
        /// Optional custom implementation detail (e.g., delegate name).
        /// </summary>
        public string? Implementation { get; init; }
    }
}
