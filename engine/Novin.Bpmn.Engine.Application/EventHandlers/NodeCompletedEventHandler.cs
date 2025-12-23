using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Commands.CompleteProcess;
using Novin.Bpmn.Engine.Application.Commands.ProcessNode;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

/// <summary>
/// Handles NodeCompletedEvent - continues the BPMN flow by moving tokens and processing next nodes
/// </summary>
public class NodeCompletedEventHandler : INotificationHandler<NodeCompletedEvent>
{
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NodeCompletedEventHandler> _logger;

    public NodeCompletedEventHandler(
        IMediator mediator,
        IUnitOfWork unitOfWork,
        ILogger<NodeCompletedEventHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(NodeCompletedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling NodeCompletedEvent for NodeId: {NodeId}, TokenId: {TokenId}", 
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

            var token = await _unitOfWork.Tokens.GetByIdAsync(@event.TokenId, cancellationToken);
            if (token == null)
            {
                _logger.LogWarning("Token not found: {TokenId}", @event.TokenId);
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return;
            }

            var process = await _unitOfWork.Processes.GetByIdAsync(@event.ProcessId, cancellationToken);
            if (process == null)
            {
                _logger.LogWarning("Process not found: {ProcessId}", @event.ProcessId);
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return;
            }

            // Get BPMN definitions to understand the flow
            var deployment = await _unitOfWork.Deployments.GetLatestByDeploymentKeyAsync(
                process.ProcessDefinitionId, cancellationToken);
            
            if (deployment == null)
            {
                _logger.LogWarning("Deployment not found for ProcessDefinitionId: {ProcessDefinitionId}", 
                    process.ProcessDefinitionId);
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return;
            }

            var bpmnDefinitions = deployment.GetDefinitions();
            var definitionsService = new BpmnDefinitionsService(bpmnDefinitions);
            var processId = definitionsService.GetFirstProcess().id ?? process.ProcessDefinitionId;

            // Handle different node types
            if (node.Type == NodeType.EndEvent)
            {
                await HandleEndEvent(node, token, process, cancellationToken);
            }
            else
            {
                await ContinueFlow(node, token, process, definitionsService, processId, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling NodeCompletedEvent for NodeId: {NodeId}", @event.NodeId);
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    private async Task HandleEndEvent(
        Domain.Entities.Node node, 
        Domain.Entities.Token token, 
        Domain.Entities.Process process,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling end event for node {NodeId}, token {TokenId}", node.Id, token.Id);

        // Complete the token
        token.Complete();
        
     

        // Check if all tokens are completed
        var allTokens = await _unitOfWork.Tokens.GetByProcessIdAsync(process.Id, cancellationToken);
        var activeTokens = allTokens.Where(t => t.State == TokenState.Active || t.State == TokenState.Waiting).ToList();

        await _unitOfWork.CommitTransactionAsync(cancellationToken);

        if (activeTokens.Count == 0)
        {
            _logger.LogInformation("All tokens completed, completing process {ProcessId}", process.Id);
            
            // Use MediatR to send CompleteProcessCommand
            var completeCommand = new CompleteProcessCommand(process.Id);
            await _mediator.Send(completeCommand, cancellationToken);
        }
    }

    private async Task ContinueFlow(
        Domain.Entities.Node node,
        Domain.Entities.Token token,
        Domain.Entities.Process process,
        BpmnDefinitionsService definitionsService,
        string processId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Continuing flow from node {NodeId} for token {TokenId}", node.Id, token.Id);

        // Get next elements from BPMN definitions
        var nextElements = definitionsService.GetNextElements(processId, node.ElementId);

        if (nextElements.Count == 0)
        {
            _logger.LogWarning("No next elements found for node {NodeId} (ElementId: {ElementId})", 
                node.Id, node.ElementId);
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return;
        }

        // Handle gateway routing (for now, simple sequential flow)
        // TODO: Implement proper gateway logic (exclusive, parallel, inclusive)
        foreach (var nextElement in nextElements)
        {
            if (string.IsNullOrEmpty(nextElement.id))
                continue;

            // Find the node for this element
            var nextNode = await _unitOfWork.Nodes.GetByElementIdAsync(process.Id, nextElement.id, cancellationToken);
            if (nextNode == null)
            {
                _logger.LogWarning("Node not found for element: {ElementId}", nextElement.id);
                continue;
            }

            // Move token to next node
            token.MoveToNextStep(nextElement.id, nextNode.Id);
            
         

            // Node receives the token
            nextNode.Reach(token.Id, nextElement.id);
         
            // Record token history through Process aggregate root
            process.RecordTokenNodeReached(token, nextNode);
            
         

            // Record node execution in process history
            process.RecordNodeExecution(nextNode.Id, nextElement.id, nextNode.NodeName, nextNode.State, token.Id);
            
         
        }

        // Commit to dispatch events
        await _unitOfWork.CommitTransactionAsync(cancellationToken);

        // Trigger ProcessNodeCommand for each next node
        foreach (var nextElement in nextElements)
        {
            if (string.IsNullOrEmpty(nextElement.id))
                continue;

            var nextNode = await _unitOfWork.Nodes.GetByElementIdAsync(process.Id, nextElement.id, cancellationToken);
            if (nextNode == null)
                continue;

            _logger.LogInformation("Triggering ProcessNodeCommand for next node: {NodeId}, Token: {TokenId}", 
                nextNode.Id, token.Id);
            
            var command = new ProcessNodeCommand(nextNode.Id, token.Id);
            await _mediator.Send(command, cancellationToken);
        }
    }
}
