using Novin.Bpmn.Models;
using System;
using System.Threading.Tasks;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.V3.Events
{
    /// <summary>
    /// Base class for all BPMN events
    /// </summary>
    public abstract class BpmnEventBase<TEventDefinition> : IBpmnEvent
    {
        public TEventDefinition TypedEventDefinition { get; }
        public object EventDefinition => TypedEventDefinition;
        public BpmnBoundaryEvent BoundaryEvent { get; }
        public BpmnV3Token Token { get; }
        public bool IsInterrupting { get; }
        public bool IsTriggered { get; protected set; }
        
        protected readonly BpmnV3ProcessInstance ProcessInstance;
        
        protected BpmnEventBase(TEventDefinition eventDefinition, BpmnBoundaryEvent boundaryEvent, BpmnV3Token token, 
            bool isInterrupting, BpmnV3ProcessInstance processInstance)
        {
            TypedEventDefinition = eventDefinition;
            BoundaryEvent = boundaryEvent;
            Token = token;
            IsInterrupting = isInterrupting;
            ProcessInstance = processInstance;
            IsTriggered = false;
        }
        
        public virtual Task Initialize()
        {
            Console.WriteLine($"Initializing {GetType().Name} for {BoundaryEvent?.id ?? "unknown"} attached to {BoundaryEvent?.attachedToRef?.Name ?? "unknown"}");
            return Task.CompletedTask;
        }
        
        public virtual async Task Trigger()
        {
            if (IsTriggered)
                return;
            
            Console.WriteLine($"Triggering {GetType().Name} for {BoundaryEvent?.id ?? "unknown"}");
            IsTriggered = true;
            
            if (IsInterrupting)
            {
                // If interrupting, we need to handle the flow differently
                Console.WriteLine($"Event is interrupting - will redirect flow from {Token.CurrentElementId}");
                await ProcessInterruptingEvent();
            }
            else
            {
                // Non-interrupting events create a new flow without affecting the original
                Console.WriteLine($"Event is non-interrupting - will create new flow while keeping original");
                await ProcessNonInterruptingEvent();
            }
        }
        
        public virtual Task Cancel()
        {
            Console.WriteLine($"Canceling {GetType().Name} for {BoundaryEvent?.id ?? "unknown"}");
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Process an interrupting event - redirects the current token to the outgoing flow
        /// </summary>
        protected virtual async Task ProcessInterruptingEvent()
        {
            // For interrupting events, we need to move the current token to the outgoing flow
            var outgoingFlows = ProcessInstance.DefinitionsHandler.GetOutgoingSequenceFlows(BoundaryEvent);
            if (outgoingFlows.Count > 0)
            {
                var flow = outgoingFlows[0]; // Take the first outgoing flow
                
                // Track that we're using this flow
                ProcessInstance.TrackFlowExecution(flow.id, Token.Id, Guid.Empty, Token.IsExecutable);
                
                // Move the token
                Token.MoveTo(flow.targetRef, flow.id);
                
                // Track that we've moved to this node
                ProcessInstance.TrackNodeExecution(flow.targetRef, Token.Id, Token.IsExecutable);
                
                // Continue process execution
                await ProcessInstance.MoveToken(Token);
            }
            else
            {
                Console.WriteLine($"Warning: No outgoing flows for interrupting event {BoundaryEvent?.id}");
            }
        }
        
        /// <summary>
        /// Process a non-interrupting event - creates a new token for the event flow
        /// </summary>
        protected virtual async Task ProcessNonInterruptingEvent()
        {
            // For non-interrupting events, we create a new token for the outgoing flow
            var outgoingFlows = ProcessInstance.DefinitionsHandler.GetOutgoingSequenceFlows(BoundaryEvent);
            if (outgoingFlows.Count > 0)
            {
                var flow = outgoingFlows[0]; // Take the first outgoing flow
                
                // Create a new token starting at the boundary event
                var newToken = ProcessInstance.CreateToken(BoundaryEvent.id);
                
                // Track that we're using this flow with the new token
                ProcessInstance.TrackFlowExecution(flow.id, newToken.Id, Guid.Empty, true);
                
                // Move the new token
                newToken.MoveTo(flow.targetRef, flow.id);
                
                // Track that we've moved to this node
                ProcessInstance.TrackNodeExecution(flow.targetRef, newToken.Id, true);
                
                // Continue process execution with the new token
                await ProcessInstance.MoveToken(newToken);
            }
            else
            {
                Console.WriteLine($"Warning: No outgoing flows for non-interrupting event {BoundaryEvent?.id}");
            }
        }
    }
} 