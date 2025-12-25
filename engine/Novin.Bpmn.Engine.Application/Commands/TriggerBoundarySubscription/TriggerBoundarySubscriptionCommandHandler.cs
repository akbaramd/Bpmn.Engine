using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Services;

namespace Novin.Bpmn.Engine.Application.Commands.TriggerBoundarySubscription;

/// <summary>
/// Handler برای TriggerBoundarySubscriptionCommand
/// این handler از IBoundaryEventExecutor استفاده می‌کند تا semantics BPMN2 را اجرا کند
/// </summary>
public sealed class TriggerBoundarySubscriptionCommandHandler 
    : IRequestHandler<TriggerBoundarySubscriptionCommand, TriggerBoundarySubscriptionResult>
{
    private readonly IBoundaryEventExecutor _executor;
    private readonly ILogger<TriggerBoundarySubscriptionCommandHandler> _logger;

    public TriggerBoundarySubscriptionCommandHandler(
        IBoundaryEventExecutor executor,
        ILogger<TriggerBoundarySubscriptionCommandHandler> logger)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TriggerBoundarySubscriptionResult> Handle(
        TriggerBoundarySubscriptionCommand request, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[TRIGGER-BOUNDARY] Triggering boundary subscription. SubscriptionId={SubscriptionId}",
            request.SubscriptionId);

        try
        {
            await _executor.ExecuteAsync(request.SubscriptionId, cancellationToken);

            return new TriggerBoundarySubscriptionResult
            {
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[TRIGGER-BOUNDARY] Failed to trigger boundary subscription. SubscriptionId={SubscriptionId}",
                request.SubscriptionId);

            return new TriggerBoundarySubscriptionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
