using System;
using System.Collections.Generic;
using Novin.Bpmn.EventSourcing.Core.Models;
using Novin.Bpmn.EventSourcing.Events;

namespace Novin.Bpmn.EventSourcing.Core.Models
{
    /// <summary>
    /// Fluent builder for <see cref="ElementExecution" /> instances.
    /// Handles initial creation via StartNew and optional lifecycle transitions.
    /// </summary>
    public class ElementExecutionBuilder
    {
        private string _processInstanceId = string.Empty;
        private string _elementId = string.Empty;
        private BpmnElementType _elementType = BpmnElementType.Unknown;
        private Dictionary<string, object>? _localVariables;
        private bool _isExecutable = true;
        private ElementExecution? _execution;

        private ElementExecutionBuilder() { }

        /// <summary>
        /// Begin building a new ElementExecution.
        /// </summary>
        public static ElementExecutionBuilder Init() => new ElementExecutionBuilder();

        /// <summary>
        /// Set the process instance ID.
        /// </summary>
        public ElementExecutionBuilder WithProcessInstanceId(string processInstanceId)
        {
            _processInstanceId = processInstanceId;
            return this;
        }

        /// <summary>
        /// Set the BPMN element ID.
        /// </summary>
        public ElementExecutionBuilder WithElementId(string elementId)
        {
            _elementId = elementId;
            return this;
        }

        /// <summary>
        /// Set the BPMN element type.
        /// </summary>
        public ElementExecutionBuilder WithElementType(BpmnElementType elementType)
        {
            _elementType = elementType;
            return this;
        }

        /// <summary>
        /// Provide optional local variables.
        /// </summary>
        public ElementExecutionBuilder WithLocalVariables(Dictionary<string, object>? localVariables)
        {
            _localVariables = localVariables;
            return this;
        }

        /// <summary>
        /// Configure whether this execution should run business logic.
        /// Defaults to true.
        /// </summary>
        public ElementExecutionBuilder Executable(bool isExecutable = true)
        {
            _isExecutable = isExecutable;
            return this;
        }

        /// <summary>
        /// Build the initial ElementExecution by invoking StartNew.
        /// </summary>
        public ElementExecutionBuilder Build()
        {
            _execution = ElementExecution.StartNew(
                _processInstanceId,
                _elementId,
                _elementType,
                _localVariables,
                _isExecutable);
            return this;
        }

        /// <summary>
        /// Mark the built execution as completed successfully.
        /// </summary>
        public ElementExecutionBuilder Complete()
        {
            EnsureBuilt();
            _execution!.Complete();
            return this;
        }

        /// <summary>
        /// Mark the built execution as failed with a reason.
        /// </summary>
        public ElementExecutionBuilder Fail(string reason)
        {
            EnsureBuilt();
            _execution!.Fail(reason);
            return this;
        }

        /// <summary>
        /// Terminate the built execution.
        /// </summary>
        public ElementExecutionBuilder Terminate()
        {
            EnsureBuilt();
            _execution!.Terminate();
            return this;
        }

        /// <summary>
        /// Suspend the built execution.
        /// </summary>
        public ElementExecutionBuilder Suspend()
        {
            EnsureBuilt();
            _execution!.Suspend();
            return this;
        }

        /// <summary>
        /// Resume a suspended or waiting execution.
        /// </summary>
        public ElementExecutionBuilder Resume()
        {
            EnsureBuilt();
            _execution!.Resume();
            return this;
        }

        /// <summary>
        /// Finalize and return the constructed ElementExecution.
        /// </summary>
        public ElementExecution BuildResult()
        {
            EnsureBuilt();
            return _execution!;
        }

        private void EnsureBuilt()
        {
            if (_execution is null)
                throw new InvalidOperationException("ElementExecution must be built before applying lifecycle operations. Call Build() first.");
        }
    }
}
