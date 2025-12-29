using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;

namespace Novin.Bpmn.Engine.Application.Commands.FailProcess;

public sealed class FailProcessCommandHandler : IRequestHandler<FailProcessCommand, FailProcessResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FailProcessCommandHandler> _logger;

    public FailProcessCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<FailProcessCommandHandler> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<FailProcessResult> Handle(FailProcessCommand request, CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var process = await _unitOfWork.Processes.GetByIdAsync(request.ProcessId, cancellationToken)
                          ?? throw new InvalidOperationException($"Process {request.ProcessId} not found.");

            process.Fail(request.Error);

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new FailProcessResult
            {
                ProcessId = process.Id,
                FailedAt = process.FailedAt!.Value,
                Error = request.Error
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark process {ProcessId} as failed", request.ProcessId);
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}

