using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;

namespace Novin.Bpmn.Engine.Application.Commands.CompleteProcess;

public class CompleteProcessCommandHandler : IRequestHandler<CompleteProcessCommand, CompleteProcessResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CompleteProcessCommandHandler> _logger;

    public CompleteProcessCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<CompleteProcessCommandHandler> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CompleteProcessResult> Handle(CompleteProcessCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Completing process: {ProcessId}", request.ProcessId);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var process = await _unitOfWork.Processes.GetByIdAsync(request.ProcessId, cancellationToken);
            
            if (process == null)
            {
                _logger.LogWarning("Process not found: {ProcessId}", request.ProcessId);
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return new CompleteProcessResult
                {
                    ProcessId = request.ProcessId,
                    Success = false
                };
            }

            process.Complete();
            
            
         
            // Track aggregate for event dispatching on commit
            
            // Commit transaction (this will dispatch events)
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.LogInformation("Process completed successfully. ProcessId: {ProcessId}", process.Id);

            return new CompleteProcessResult
            {
                ProcessId = process.Id,
                CompletedAt = process.CompletedAtUtc!.Value,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing process: {ProcessId}", request.ProcessId);
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}

