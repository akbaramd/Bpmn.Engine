using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using MediatR;

namespace Novin.Bpmn.Engine.Application.Features.Workers.Commands;

public class FailWorkerCommandHandler
    : IRequestHandler<FailWorkerCommand>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWorkerRepository _workerRepository;
    private readonly ILogger<FailWorkerCommandHandler> _logger;

    public FailWorkerCommandHandler(
        IUnitOfWork unitOfWork,
        IWorkerRepository workerRepository,
        ILogger<FailWorkerCommandHandler> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _workerRepository = workerRepository ?? throw new ArgumentNullException(nameof(workerRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(FailWorkerCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Failing worker {WorkerId} with error: {Error}", request.WorkerId, request.Error);

        await _unitOfWork.ExecuteInTransactionAsync(async trxCt =>
        {
            // Get the worker
            var worker = await _workerRepository.GetByIdAsync(request.WorkerId, trxCt);
            if (worker == null)
            {
                _logger.LogWarning("Job {WorkerId} not found", request.WorkerId);
                return;
            }

            // Mark worker as failed - this will raise WorkerFailedEvent
            // which can be handled by WorkerFailedEventHandler for retry logic, process failure, etc.
            worker.Fail(request.Error);
            await _workerRepository.UpdateAsync(worker, trxCt);

            _logger.LogInformation("Job {WorkerId} marked as failed", request.WorkerId);
        }, cancellationToken);
    }
}