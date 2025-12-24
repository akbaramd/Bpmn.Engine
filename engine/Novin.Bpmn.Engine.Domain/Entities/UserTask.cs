using System;
using System.Collections.Generic;
using Novin.Bpmn.Engine.Domain.Common;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using TaskStatus = Novin.Bpmn.Engine.Domain.ValueObjects.TaskStatus;

namespace Novin.Bpmn.Engine.Domain.Entities
{
    /// <summary>
    /// Aggregate root representing a BPMN task
    /// </summary>
    public class UserTask : BaseAggregateRoot
    {
        public Guid ProcessId { get; private set; }
        public string Name { get; private set; }
        public string ElementId { get; private set; }
        public TaskStatus Status { get; private set; }
        public Dictionary<string, object> InputVariables { get; private set; }
        public Dictionary<string, object> OutputVariables { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? StartedAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }
        public string? AssignedTo { get; private set; }

        // Goods associated with the UserTask (e.g., products related to this task)
        private UserTask() : base()
        {
            InputVariables = new Dictionary<string, object>();
            OutputVariables = new Dictionary<string, object>();
            Status = TaskStatus.Created;
            CreatedAt = DateTime.UtcNow;
        }

        public UserTask(Guid processId, string name, string elementId) : this()
        {
            if (processId == Guid.Empty)
                throw new ArgumentException("Process ID cannot be empty", nameof(processId));

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Task name cannot be null or empty", nameof(name));

            if (string.IsNullOrWhiteSpace(elementId))
                throw new ArgumentException("Element ID cannot be null or empty", nameof(elementId));

            ProcessId = processId;
            Name = name;
            ElementId = elementId;
        }

        // -------------------- Lifecycle --------------------

        public void Activate()
        {
            if (Status != TaskStatus.Created && Status != TaskStatus.Ready)
                throw new InvalidOperationException($"Cannot activate task in {Status} status.");

            Status = TaskStatus.Active;
            StartedAt = DateTime.UtcNow;
        }

        public void Complete(Dictionary<string, object>? outputVariables = null)
        {
            if (Status != TaskStatus.Active)
                throw new InvalidOperationException($"Cannot complete task in {Status} status. Task must be Active.");

            Status = TaskStatus.Completed;
            CompletedAt = DateTime.UtcNow;

            if (outputVariables != null)
            {
                foreach (var variable in outputVariables)
                {
                    OutputVariables[variable.Key] = variable.Value;
                }
            }
        }

        public void Fail(string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
                throw new ArgumentException("Error message cannot be null or empty", nameof(errorMessage));

            Status = TaskStatus.Failed;
        }

        public void Terminate()
        {
            if (Status == TaskStatus.Completed)
                throw new InvalidOperationException("Cannot terminate a completed task.");

            Status = TaskStatus.Terminated;
        }

        public void Assign(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

            AssignedTo = userId;
        }

        public void SetInputVariable(string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Variable key cannot be null or empty", nameof(key));

            InputVariables[key] = value;
        }

        
    }
}
