using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Models.Models;
using Task = System.Threading.Tasks.Task;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

/// <summary>
/// Handles ProcessStartedEvent - finds start nodes, creates tokens if needed, and processes them
/// </summary>
public class ProcessStartedEventHandler : INotificationHandler<ProcessStartedEvent>
{
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProcessStartedEventHandler> _logger;

    public ProcessStartedEventHandler(
        IMediator mediator,
        IUnitOfWork unitOfWork,
        ILogger<ProcessStartedEventHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(ProcessStartedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling ProcessStartedEvent for ProcessId: {ProcessId}", @event.ProcessId);

        // 1. We start a transaction for the setup phase (Creating Tokens)
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var process = await _unitOfWork.Processes.GetByIdAsync(@event.ProcessId, cancellationToken);
            if (process == null)
            {
                _logger.LogWarning("Process with ID {ProcessId} not found.", @event.ProcessId);
                return;
            }

            var deployment = await _unitOfWork.Deployments.GetLatestByDeploymentKeyAsync(
                process.ProcessDefinitionId, cancellationToken);

            if (deployment == null)
            {
                _logger.LogWarning("Deployment for ProcessDefinitionId {ProcessDefinitionId} not found.", process.ProcessDefinitionId);
                return;
            }

            var bpmnDefinitions = deployment.GetDefinitions();
            var definitionsService = new BpmnDefinitionsService(bpmnDefinitions);
            var bpmnProcessId = definitionsService.GetFirstProcess().id ?? process.ProcessDefinitionId;
            var startEvents = definitionsService.GetStartEvents(bpmnProcessId);

            var tokensToProcess = new List<(string NodeId, Guid TokenId)>();

            foreach (var startEvent in startEvents)
            {
                if (string.IsNullOrEmpty(startEvent.id))
                    continue;

                // Check existing tokens
                var existingTokens = await _unitOfWork.Tokens.GetByProcessIdAsync(@event.ProcessId, cancellationToken);
                var tokensAtNode = existingTokens.Where(t => t.CurrentElementId == startEvent.id).ToList();

                if (tokensAtNode.Any())
                {
                    tokensToProcess.AddRange(tokensAtNode.Select(t => (startEvent.id, t.Id)));
                }
                else
                {
                    // Internal method now just prepares the entities in the DbContext
                    var newToken = await PrepareTokenAtStartNode(process, startEvent, startEvent.id, cancellationToken);
                    tokensToProcess.Add((startEvent.id, newToken.Id));
                }
            }

            // 2. Commit the setup phase (Tokens are now in the DB)
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

          
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling ProcessStartedEvent for ProcessId: {ProcessId}", @event.ProcessId);
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    private async Task<Token> PrepareTokenAtStartNode(
        Domain.Entities.Process process,
        BpmnStartEvent startNode,
        string elementId,
        CancellationToken cancellationToken)
    {
        var token = new Token(process.Id, elementId, []);
        await _unitOfWork.Tokens.AddAsync(token, cancellationToken);

        token.Activate();
        process.AddToken(token.Id);
        return token;
    }
}
