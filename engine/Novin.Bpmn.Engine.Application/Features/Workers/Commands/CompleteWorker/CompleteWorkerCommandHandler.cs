using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using MediatR;

namespace Novin.Bpmn.Engine.Application.Features.Workers.Commands;

public class CompleteWorkerCommandHandler
    : IRequestHandler<CompleteWorkerCommand>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWorkerRepository _workerRepository;
    private readonly ILogger<CompleteWorkerCommandHandler> _logger;

    public CompleteWorkerCommandHandler(
        IUnitOfWork unitOfWork,
        IWorkerRepository workerRepository,
        ILogger<CompleteWorkerCommandHandler> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _workerRepository = workerRepository ?? throw new ArgumentNullException(nameof(workerRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(CompleteWorkerCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Completing worker {WorkerId}", request.WorkerId);

        await _unitOfWork.ExecuteInTransactionAsync(async trxCt =>
        {
            // Get the worker
            var worker = await _workerRepository.GetByIdAsync(request.WorkerId, trxCt);
            if (worker == null)
            {
                _logger.LogWarning("Job {WorkerId} not found", request.WorkerId);
                return;
            }

            // Mark worker as completed - this will raise WorkerCompletedEvent
            // which will be handled by WorkerCompletedEventHandler
            worker.Succeed(request.Result);
            await _workerRepository.UpdateAsync(worker, trxCt);

            _logger.LogInformation("Job {WorkerId} marked as completed", request.WorkerId);
        }, cancellationToken);
    }
}