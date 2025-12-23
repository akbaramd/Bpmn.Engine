using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;

namespace Novin.Bpmn.Engine.Application.Commands.CompleteNode;

public class CompleteNodeCommandHandler : IRequestHandler<CompleteNodeCommand, CompleteNodeResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CompleteNodeCommandHandler> _logger;

    public CompleteNodeCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<CompleteNodeCommandHandler> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CompleteNodeResult> Handle(CompleteNodeCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Completing node: {NodeId} with token: {TokenId}", request.NodeId, request.TokenId);

        // Check if transaction is already active (e.g., from event handler)
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var node = await _unitOfWork.Nodes.GetByIdAsync(request.NodeId, cancellationToken);
            
            if (node == null)
            {
                _logger.LogWarning("Node not found: {NodeId}", request.NodeId);
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return new CompleteNodeResult
                {
                    NodeId = request.NodeId,
                    Success = false
                };
            }

            if (!request.TokenId.HasValue)
            {
                _logger.LogWarning("TokenId is required for completing node: {NodeId}", request.NodeId);
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return new CompleteNodeResult
                {
                    NodeId = request.NodeId,
                    Success = false
                };
            }

            node.Complete(request.TokenId.Value, request.OutputVariables);
            
            
         
            // Track aggregate for event dispatching on commit
            
            
            // Only commit if we started the transaction
            // If transaction was already active, let the caller commit it
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.LogInformation("Node completed successfully. NodeId: {NodeId}", node.Id);

            return new CompleteNodeResult
            {
                NodeId = node.Id,
                CompletedAt = node.CompletedAt!.Value,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing node: {NodeId}", request.NodeId);
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}

