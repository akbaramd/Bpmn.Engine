using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.Commands.CreateNode;

public class CreateNodeCommandHandler : IRequestHandler<CreateNodeCommand, CreateNodeResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateNodeCommandHandler> _logger;

    public CreateNodeCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<CreateNodeCommandHandler> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CreateNodeResult> Handle(CreateNodeCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating node: {NodeName} ({ElementId}) for process: {ProcessId}", 
            request.NodeName, request.ElementId, request.ProcessId);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var node = new Node(request.ProcessId, request.NodeName, request.ElementId, request.NodeType);
            
            await _unitOfWork.Nodes.AddAsync(node, cancellationToken);
         
            // Track aggregate for event dispatching on commit
            
            
            // Commit transaction (this will dispatch events)
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.LogInformation("Node created successfully. NodeId: {NodeId}", node.Id);

            return new CreateNodeResult
            {
                NodeId = node.Id,
                NodeName = node.NodeName,
                ElementId = node.ElementId,
                CreatedAt = node.CreatedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating node: {NodeName}", request.NodeName);
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}

