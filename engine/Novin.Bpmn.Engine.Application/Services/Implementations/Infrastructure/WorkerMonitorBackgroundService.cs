using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.Services;

/// <summary>
/// Background service that monitors workers for timeouts and retries
/// </summary>
public class WorkerMonitorBackgroundService : BackgroundService
{
    private readonly IWorkerRepository _workerRepository;
    private readonly IClientCommunicationService _clientCommunication;
    private readonly ILogger<WorkerMonitorBackgroundService> _logger;

    // Check every 30 seconds
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);
    // Timeout after 5 minutes
    private readonly TimeSpan _timeoutThreshold = TimeSpan.FromMinutes(5);

    public WorkerMonitorBackgroundService(
        IWorkerRepository workerRepository,
        IClientCommunicationService clientCommunication,
        ILogger<WorkerMonitorBackgroundService> logger)
    {
        _workerRepository = workerRepository ?? throw new ArgumentNullException(nameof(workerRepository));
        _clientCommunication = clientCommunication ?? throw new ArgumentNullException(nameof(clientCommunication));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker Monitor Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckForTimedOutWorkersAsync(stoppingToken);
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in worker monitor");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        _logger.LogInformation("Worker Monitor Background Service stopped");
    }

    private async Task CheckForTimedOutWorkersAsync(CancellationToken cancellationToken)
    {
        // Get workers that are in progress and have been running longer than the timeout
        var timedOutWorkers = await _workerRepository.GetByStatusAsync(WorkerStatus.InProgress, cancellationToken);
        var now = DateTime.UtcNow;

        var actuallyTimedOut = timedOutWorkers.Where(w =>
            w.StartedAtUtc.HasValue &&
            (now - w.StartedAtUtc.Value) > _timeoutThreshold).ToList();

        foreach (var worker in actuallyTimedOut)
        {
            _logger.LogWarning("Worker {WorkerId} ({TaskName}) timed out after {Timeout}",
                worker.Id, worker.TaskName, _timeoutThreshold);

            try
            {
                // Mark as timed out
                worker.MarkTimedOut();
                await _workerRepository.UpdateAsync(worker, cancellationToken);

                // TODO: Could implement retry logic here
                // For now, just mark as timed out

                _logger.LogInformation("Worker {WorkerId} marked as timed out", worker.Id);
            }
            catch (Exception ex)
            {
                // Check if this is a foreign key constraint violation (orphaned worker)
                if (ex.Message.Contains("FOREIGN KEY constraint failed") ||
                    ex.InnerException?.Message.Contains("FOREIGN KEY constraint failed") == true)
                {
                    _logger.LogWarning("Worker {WorkerId} appears to be orphaned (referenced Process/Token deleted). Deleting worker.", worker.Id);

                    try
                    {
                        // Delete the orphaned worker
                        await _workerRepository.DeleteAsync(worker.Id, cancellationToken);
                        _logger.LogInformation("Deleted orphaned worker {WorkerId}", worker.Id);
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogError(deleteEx, "Failed to delete orphaned worker {WorkerId}", worker.Id);
                    }
                }
                else
                {
                _logger.LogError(ex, "Error processing timeout for worker {WorkerId}", worker.Id);
                }
            }
        }

        if (actuallyTimedOut.Any())
        {
            _logger.LogInformation("Processed {Count} timed out workers", actuallyTimedOut.Count);
        }
    }
}