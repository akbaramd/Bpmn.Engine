using System;
using System.Collections.Generic;

namespace Novin.Bpmn
{
    public class BpmnV3Token
    {
        // Unique identifier for the token
        public Guid Id { get; private set; }

        // Parent token ID for forked tokens
        public Guid? ParentTokenId { get; set; }

        // Current element ID where the token resides
        public string CurrentElementId { get; private set; }

        // Indicates if the token is active
        public bool IsExecutable { get; private set; } = true;

        // Tracks the lifecycle status of the token
        public TokenStatus Status { get; private set; } = TokenStatus.Active;

        // History of token movements
        public List<TokenHistoryEntry> History { get; private set; } = new List<TokenHistoryEntry>();

        // Constructor to initialize the token
        public BpmnV3Token(string startElementId,string? startFlowId = null)
        {
            Id = Guid.NewGuid();
            CurrentElementId = startElementId;
            AddToHistory(startFlowId, startElementId); // Add initial element to history
        }

        // Moves the token to a new element
        public void MoveTo(string elementId, string flowId = null)
        {
            AddToHistory(flowId, elementId);
            CurrentElementId = elementId;
            Status = TokenStatus.Active; // Token becomes active on move
        }

        // Deactivates the token
        public void UnExecutable()
        {
            IsExecutable = false;
        }

        public void Executable()
        {
            IsExecutable = true;
        }

        // Reactivates the token
        public void Reactivate()
        {
            Status = TokenStatus.Active;
        }

        // Completes the token lifecycle
        public void Complete()
        {
            Status = TokenStatus.Completed;
        }

        // Expires the token
        public void Expire()
        {
            Status = TokenStatus.Expired;
        }

        public override string ToString()
        {
            return $"Token {Id}: CurrentElementId={CurrentElementId}, IsExecutable={IsExecutable}, Status={Status}";
        }

        public void SetPendingToMerge()
        {
            Status = TokenStatus.PendingToMerge;
        }

        public void SetWaiting()
        {
            Status = TokenStatus.Waiting;
        }

        // Adds a record to the token's history
        private void AddToHistory(string flowId, string elementId)
        {
            History.Add(new TokenHistoryEntry
            {
                FlowId = flowId,
                ElementId = elementId,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    public enum TokenStatus
    {
        Active, // Ready for processing
        Waiting, // Waiting for merge in post
        Completed, // Process completed
        Expired, // Token expired
        PendingToMerge
    }

    // Represents a single history entry for a token
    public class TokenHistoryEntry
    {
        public string FlowId { get; set; } // ID of the flow
        public string ElementId { get; set; } // ID of the element the token moved to
        public DateTime Timestamp { get; set; } // Time of the move

        public override string ToString()
        {
            return $"{FlowId } -> {ElementId} at {Timestamp:O}";
        }
    }
}
