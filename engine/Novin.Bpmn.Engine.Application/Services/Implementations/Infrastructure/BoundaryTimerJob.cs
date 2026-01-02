using Quartz;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Services.Implementations.Infrastructure;

[DisallowConcurrentExecution]
public sealed class BoundaryTimerJob : IJob
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<BoundaryTimerJob> _logger;

    public BoundaryTimerJob(IServiceProvider sp, ILogger<BoundaryTimerJob> logger)
    {
        _sp = sp ?? throw new ArgumentNullException(nameof(sp));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var subIdText = context.MergedJobDataMap.GetString("SubscriptionId");

        // IMPORTANT: you stored as "N" => parse as "N"
        if (string.IsNullOrWhiteSpace(subIdText) || !Guid.TryParseExact(subIdText, "N", out var subscriptionId))
        {
            _logger.LogError("[TIMER-JOB] Invalid SubscriptionId in job data: {SubscriptionId}", subIdText);
            return;
        }

        using var scope = _sp.CreateScope();

        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var timerScheduler = scope.ServiceProvider.GetRequiredService<ITimerScheduler>();

        try
        {
            await uow.ExecuteInTransactionAsync(async ct =>
            {
                var sub = await uow.BoundarySubscriptions.GetByIdAsync(subscriptionId, ct);

                if (sub is null)
                {
                    _logger.LogWarning("[TIMER-JOB] Subscription not found. Unscheduling. SubId={SubId}", subscriptionId);
                    await timerScheduler.UnscheduleAsync(subscriptionId, ct);
                    return;
                }

                // If your aggregate has IsActive/State, use it here:
                if (sub.State != SubscriptionState.Active) // adjust if your model differs
                {
                    _logger.LogInformation("[TIMER-JOB] Subscription inactive. Unscheduling. SubId={SubId}", subscriptionId);
                    await timerScheduler.UnscheduleAsync(subscriptionId, ct);
                    return;
                }

                _logger.LogInformation(
                    "[TIMER-JOB] Timer fired. SubId={SubId} ProcessId={ProcessId} TokenId={TokenId} Boundary={BoundaryId}",
                    sub.Id, sub.ProcessId, sub.TokenId, sub.BoundaryElementId);

                // ✅ Publish with REAL data (no Guid.Empty / empty strings)
                sub.MarkTriggered();

            }, context.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TIMER-JOB] Failed executing timer job. SubId={SubId}", subscriptionId);
            throw; // let Quartz see failure (or swallow if you prefer)
        }
    }
}
