using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Commands.CompleteNode;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Task = System.Threading.Tasks.Task;
using TaskStatus = Novin.Bpmn.Engine.Domain.ValueObjects.TaskStatus;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

/// <summary>
/// Handles NodeProcessingEvent - processes the node based on its type
/// System tasks (ServiceTask, ScriptTask) complete immediately
/// UserTask creates a Task entity and waits for user completion
/// </summary>
public class NodeProcessingEventHandler : INotificationHandler<NodeProcessingEvent>
{
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NodeProcessingEventHandler> _logger;

    public NodeProcessingEventHandler(
        IMediator mediator,
        IUnitOfWork unitOfWork,
        ILogger<NodeProcessingEventHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(NodeProcessingEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling NodeProcessingEvent for NodeId: {NodeId}, TokenId: {TokenId}", 
            @event.NodeId, @event.TokenId);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var node = await _unitOfWork.Nodes.GetByIdAsync(@event.NodeId, cancellationToken);
            if (node == null)
            {
                _logger.LogWarning("Node not found: {NodeId}", @event.NodeId);
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return;
            }

            // Process node based on its type
            switch (node.Type)
            {
                case NodeType.StartEvent:
                case NodeType.EndEvent:
                case NodeType.IntermediateEvent:
                    // These events complete immediately
                    await CompleteNodeImmediately(@event.NodeId, @event.TokenId, cancellationToken);
                    break;

                case NodeType.ServiceTask:
                case NodeType.ScriptTask:
                case NodeType.Task:
                case NodeType.ManualTask:
                    // System tasks execute and complete immediately (synchronous)
                    _logger.LogInformation("Processing system task {NodeId} (Type: {NodeType})", 
                        node.Id, node.Type);
                    await CompleteNodeImmediately(@event.NodeId, @event.TokenId, cancellationToken);
                    break;

                case NodeType.UserTask:
                    // User tasks create Task entity and wait for user input
                    await HandleUserTask(node, @event.TokenId, cancellationToken);
                    break;

                case NodeType.ExclusiveGateway:
                case NodeType.InclusiveGateway:
                case NodeType.ParallelGateway:
                case NodeType.EventBasedGateway:
                case NodeType.Gateway:
                    // Gateways complete immediately after routing
                    await CompleteNodeImmediately(@event.NodeId, @event.TokenId, cancellationToken);
                    break;

                case NodeType.SubProcess:
                    // SubProcess handling would go here
                    _logger.LogInformation("SubProcess {NodeId} processing started", node.Id);
                    await CompleteNodeImmediately(@event.NodeId, @event.TokenId, cancellationToken);
                    break;

                default:
                    // For unknown types, complete immediately
                    _logger.LogWarning("Unknown node type {NodeType} for node {NodeId}, completing immediately", 
                        node.Type, @event.NodeId);
                    await CompleteNodeImmediately(@event.NodeId, @event.TokenId, cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling NodeProcessingEvent for NodeId: {NodeId}", @event.NodeId);
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    private async Task HandleUserTask(Node node, Guid tokenId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling UserTask {NodeId} for token {TokenId}", node.Id, tokenId);

        // Check if Task entity already exists for this node
        var existingTask = await _unitOfWork.Tasks.GetByElementIdAsync(node.ProcessId, node.ElementId, cancellationToken);
        
        if (existingTask == null)
        {
            // Create new Task entity for UserTask
            var task = new Domain.Entities.Task(node.ProcessId, node.NodeName, node.ElementId);
            
            // Copy input variables from node to task
            foreach (var variable in node.Variables)
            {
                task.SetInputVariable(variable.Key, variable.Value);
            }
            
            await _unitOfWork.Tasks.AddAsync(task, cancellationToken);
         
            // Activate the task
            task.Activate();
         
            _logger.LogInformation("Created and activated Task entity {TaskId} for UserTask {NodeId}", 
                task.Id, node.Id);
        }
        else
        {
            // Task already exists, activate it if needed
            if (existingTask.Status != TaskStatus.Active)
            {
                existingTask.Activate();
                
             
                _logger.LogInformation("Activated existing Task entity {TaskId} for UserTask {NodeId}", 
                    existingTask.Id, node.Id);
            }
        }

        await _unitOfWork.CommitTransactionAsync(cancellationToken);
        
        // UserTask waits for user completion - don't auto-complete
        _logger.LogInformation("UserTask {NodeId} is waiting for user input. Task entity created/activated.", node.Id);
    }

    private async Task CompleteNodeImmediately(Guid nodeId, Guid tokenId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Completing node {NodeId} immediately for token {TokenId}", nodeId, tokenId);
        
        await _unitOfWork.CommitTransactionAsync(cancellationToken);
        
        var command = new CompleteNodeCommand(nodeId, tokenId);
        await _mediator.Send(command, cancellationToken);
    }
}

