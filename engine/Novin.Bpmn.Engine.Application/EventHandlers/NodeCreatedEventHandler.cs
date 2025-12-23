using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Commands.ProcessNode;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Task = System.Threading.Tasks.Task;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

/// <summary>
/// Handles NodeCreatedEvent - for start event nodes, creates tokens and processes them if process is running
/// </summary>
public class NodeCreatedEventHandler : INotificationHandler<NodeCreatedEvent>
{
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NodeCreatedEventHandler> _logger;

    public NodeCreatedEventHandler(
        IMediator mediator,
        IUnitOfWork unitOfWork,
        ILogger<NodeCreatedEventHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(NodeCreatedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling NodeCreatedEvent for NodeId: {NodeId}, NodeType: {NodeType}", 
            @event.NodeId, @event.NodeType);

        try
        {
            // Only automatically process start event nodes
            if (@event.NodeType != NodeType.StartEvent)
            {
                _logger.LogDebug("Node {NodeId} is not a start event, skipping automatic processing", @event.NodeId);
                return;
            }

            // Check if process is already started
            var process = await _unitOfWork.Processes.GetByIdAsync(@event.ProcessId, cancellationToken);
            if (process == null)
            {
                _logger.LogWarning("Process not found: {ProcessId}", @event.ProcessId);
                return;
            }

            // Only process if the process is running
            if (process.State != ProcessState.Running)
            {
                _logger.LogDebug("Process {ProcessId} is not running yet (State: {State}), will process node when process starts", 
                    @event.ProcessId, process.State);
                return;
            }

            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                // Get the node
                var node = await _unitOfWork.Nodes.GetByIdAsync(@event.NodeId, cancellationToken);
                if (node == null)
                {
                    _logger.LogWarning("Node not found: {NodeId}", @event.NodeId);
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return;
                }

                // Check if we already have tokens at this node
                var existingTokens = await _unitOfWork.Tokens.GetByProcessIdAsync(@event.ProcessId, cancellationToken);
                var tokensAtNode = existingTokens.Where(t => t.CurrentNodeId == @event.NodeId).ToList();

                if (tokensAtNode.Any())
                {
                    // Process existing tokens
                    foreach (var token in tokensAtNode)
                    {
                        _logger.LogInformation("Processing existing token {TokenId} at start event node: {NodeId}", 
                            token.Id, @event.NodeId);
                        
                        await _unitOfWork.CommitTransactionAsync(cancellationToken);
                        
           
                        
                        await _unitOfWork.BeginTransactionAsync(cancellationToken);
                    }
                }
                else
                {
                    // Create a new token for this start event node
                    _logger.LogInformation("Creating new token for start event node: {NodeId} (ElementId: {ElementId})", 
                        node.Id, node.ElementId);
                    
                    var token = new Token(process.Id, node.ElementId, node.Id);
                    await _unitOfWork.Tokens.AddAsync(token, cancellationToken);
                 
                    
                    // Activate token
                    token.Activate();
                    
                 
                    // Add token to process
                    process.AddToken(token.Id);
                    
                    // Node receives the token
                    node.Reach(token.Id, node.ElementId);
                    
                 
                    
                    // Record token history through Process aggregate root
                    process.RecordTokenNodeReached(token, node);
                    
                 
                    // Record node execution in process history
                    process.RecordNodeExecution(node.Id, node.ElementId, node.NodeName, node.State, token.Id);
                    
                 
                    // Commit to dispatch events
                    await _unitOfWork.CommitTransactionAsync(cancellationToken);
                    
                    // Trigger ProcessNodeCommand for the start node
                    _logger.LogInformation("Triggering ProcessNodeCommand for start event node: {NodeId}, Token: {TokenId}", 
                        node.Id, token.Id);
                    
                    var command = new ProcessNodeCommand(node.Id, token.Id);
                    await _mediator.Send(command, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling NodeCreatedEvent for NodeId: {NodeId}", @event.NodeId);
            throw;
        }
    }
}

