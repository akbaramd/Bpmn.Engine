using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;

namespace Novin.Bpmn.Engine.Application.Commands.ResumeProcess;

public sealed class ResumeProcessCommandHandler : IRequestHandler<ResumeProcessCommand, ResumeProcessResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ResumeProcessCommandHandler> _logger;

    public ResumeProcessCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<ResumeProcessCommandHandler> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ResumeProcessResult> Handle(ResumeProcessCommand request, CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var process = await _unitOfWork.Processes.GetByIdAsync(request.ProcessId, cancellationToken)
                          ?? throw new InvalidOperationException($"Process {request.ProcessId} not found.");

            var resumedAt = DateTime.UtcNow;
            process.Resume();

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new ResumeProcessResult
            {
                ProcessId = process.Id,
                ResumedAt = resumedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resume process {ProcessId}", request.ProcessId);
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}

