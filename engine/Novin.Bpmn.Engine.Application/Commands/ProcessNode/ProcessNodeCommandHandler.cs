using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;

namespace Novin.Bpmn.Engine.Application.Commands.ProcessNode;

public class ProcessNodeCommandHandler : IRequestHandler<ProcessNodeCommand, ProcessNodeResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProcessNodeCommandHandler> _logger;

    public ProcessNodeCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<ProcessNodeCommandHandler> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ProcessNodeResult> Handle(ProcessNodeCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing node: {NodeId} with token: {TokenId}", request.NodeId, request.TokenId);

        // Check if transaction is already active (e.g., from event handler)
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var node = await _unitOfWork.Nodes.GetByIdAsync(request.NodeId, cancellationToken);
            
            if (node == null)
            {
                _logger.LogWarning("Node not found: {NodeId}", request.NodeId);
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    
                return new ProcessNodeResult
                {
                    NodeId = request.NodeId,
                    Success = false
                };
            }

            if (!request.TokenId.HasValue)
            {
                _logger.LogWarning("TokenId is required for processing node: {NodeId}", request.NodeId);
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return new ProcessNodeResult
                {
                    NodeId = request.NodeId,
                    Success = false
                };
            }

            node.StartProcessing(request.TokenId.Value);
            
            
         
            // Track aggregate for event dispatching on commit
            
            
            // Only commit if we started the transaction
            // If transaction was already active, let the caller commit it
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.LogInformation("Node processing started successfully. NodeId: {NodeId}", node.Id);

            return new ProcessNodeResult
            {
                NodeId = node.Id,
                StartedAt = node.ProcessingStartedAt!.Value,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing node: {NodeId}", request.NodeId);
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}

