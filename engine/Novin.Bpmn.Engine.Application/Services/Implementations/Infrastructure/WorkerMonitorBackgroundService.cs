using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.Services;

public sealed class WorkerMonitorBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkerMonitorBackgroundService> _logger;

    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _timeoutThreshold = TimeSpan.FromMinutes(5);

    public WorkerMonitorBackgroundService(
        IServiceScopeFactory scopeFactory,
        IClientCommunicationService clientCommunication,
        ILogger<WorkerMonitorBackgroundService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Job Monitor Background Service started");

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

        _logger.LogInformation("Job Monitor Background Service stopped");
    }

    private async Task CheckForTimedOutWorkersAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var workerRepository = scope.ServiceProvider.GetRequiredService<IWorkerRepository>();
        // اگر IClientCommunicationService هم Scoped بود:
        // var clientCommunication = scope.ServiceProvider.GetRequiredService<IClientCommunicationService>();

        var timedOutWorkers = await workerRepository.GetByStatusAsync(JobStatus.Running, cancellationToken);
        var now = DateTime.UtcNow;

        var actuallyTimedOut = timedOutWorkers
            .Where(w => w.StartedAtUtc.HasValue && (now - w.StartedAtUtc.Value) > _timeoutThreshold)
            .ToList();

        foreach (var worker in actuallyTimedOut)
        {
            _logger.LogWarning("Job {WorkerId} ({TaskName}) timed out after {Timeout}",
                worker.Id, worker.TaskName, _timeoutThreshold);

            try
            {
                worker.Fail("timed out");
                await workerRepository.UpdateAsync(worker, cancellationToken);

                // نمونه: اطلاع‌رسانی (اختیاری)
                // await clientCommunication.NotifyAsync(...);

                _logger.LogInformation("Job {WorkerId} marked as timed out", worker.Id);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("FOREIGN KEY constraint failed") ||
                    ex.InnerException?.Message.Contains("FOREIGN KEY constraint failed") == true)
                {
                    _logger.LogWarning("Job {WorkerId} appears orphaned. Deleting worker.", worker.Id);

                    try
                    {
                        await workerRepository.DeleteAsync(worker.Id, cancellationToken);
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

        if (actuallyTimedOut.Count > 0)
            _logger.LogInformation("Processed {Count} timed out workers", actuallyTimedOut.Count);
    }
}
