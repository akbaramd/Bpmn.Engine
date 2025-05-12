using System;
using System.Collections.Generic;
using System.Linq;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Events;

namespace Novin.Bpmn.EventSourcing.Core.Models
{
    /// <summary>
    /// Mutable state for one gateway execution: tracks incoming tokens, 
    /// outgoing branches, and attached events. 
    /// Does NOT evaluate conditions or perform forks/merges itself.
    /// </summary>
    public class GatewayExecution
    {
        public string ExecutionId { get; private set; }
        public string ProcessInstanceId { get; private set; }
        public string ElementId { get; private set; }
        public BpmnElementType ElementType { get; private set; }

        /// <summary>Flows to join</summary>
        public IReadOnlyList<string> IncomingFlowIds { get; }

        /// <summary>Flows to split</summary>
        public IReadOnlyList<string> OutgoingFlowIds { get; }

        /// <summary>Counts tokens per incoming flow</summary>
        private readonly Dictionary<string,int> _tokenCounts;

        /// <summary>Placeholders for forked child executions</summary>
        private readonly List<string> _forkedFlowIds = new();

        /// <summary>Event history</summary>
        public List<IBpmnEvent> Events { get; } = new();

        public GatewayExecution(
            string executionId,
            string processInstanceId,
            string elementId,
            BpmnElementType elementType,
            IEnumerable<string> incomingFlowIds,
            IEnumerable<string> outgoingFlowIds)
        {
            ExecutionId       = executionId;
            ProcessInstanceId = processInstanceId;
            ElementId         = elementId;
            ElementType       = elementType;
            IncomingFlowIds   = incomingFlowIds.ToList();
            OutgoingFlowIds   = outgoingFlowIds.ToList();

            _tokenCounts = IncomingFlowIds.ToDictionary(f => f, _ => 0);
        }

        /// <summary>
        /// Record that one token arrived on that flow.
        /// Does NOT decide merge—just increments the count.
        /// </summary>
        public void RecordToken(string incomingFlowId)
        {
            if (!_tokenCounts.ContainsKey(incomingFlowId))
                throw new ArgumentException($"'{incomingFlowId}' not an incoming flow.");

            _tokenCounts[incomingFlowId]++;
        }

        /// <summary>
        /// Total tokens received so far.
        /// </summary>
        public int TotalReceivedTokens => _tokenCounts.Values.Sum();

        /// <summary>
        /// How many distinct flows we expect to merge.
        /// </summary>
        public int ExpectedTokens => IncomingFlowIds.Count;

        /// <summary>
        /// Whether all required tokens have arrived.
        /// </summary>
        public bool CanMerge => TotalReceivedTokens >= ExpectedTokens;

        /// <summary>
        /// Mark that we forked along this outgoing flow.
        /// </summary>
        public void MarkForked(string flowId)
        {
            if (!OutgoingFlowIds.Contains(flowId))
                throw new ArgumentException($"'{flowId}' not an outgoing flow.");

            if (!_forkedFlowIds.Contains(flowId))
                _forkedFlowIds.Add(flowId);
        }

        /// <summary>
        /// Which flows have already been forked.
        /// </summary>
        public IReadOnlyList<string> ForkedFlowIds => _forkedFlowIds;

        /// <summary>
        /// Attach any gateway‐related event.
        /// </summary>
        public void AddEvent(IBpmnEvent evt)
        {
            Events.Add(evt);
        }
    }
}
