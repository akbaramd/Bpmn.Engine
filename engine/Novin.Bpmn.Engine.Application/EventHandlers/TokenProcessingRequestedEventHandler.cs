using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Domain.Events;

public sealed class TokenProcessingRequestedEventHandler
    : INotificationHandler<TokenProcessingRequestedEvent>
{
    private readonly ITokenProcessingOrchestrator _orchestrator;
    private readonly ILogger<TokenProcessingRequestedEventHandler> _logger;

    public TokenProcessingRequestedEventHandler(
        ITokenProcessingOrchestrator orchestrator,
        ILogger<TokenProcessingRequestedEventHandler> logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(TokenProcessingRequestedEvent n, CancellationToken ct)
    {
        try
        {
            await _orchestrator.ProcessAsync(n.ProcessId, n.TokenId, ct);
        }
        catch (Exception ex)
        {
            // این catch فقط برای safety است - در حالت عادی نباید exception به اینجا برسد
            // چون Orchestrator همه exceptions را handle می‌کند
            _logger.LogError(
                ex,
                "[TOKEN-PROCESSING] ⚠️ Unexpected unhandled exception in token processing pipeline. ProcessId={ProcessId} TokenId={TokenId}",
                n.ProcessId,
                n.TokenId);

            // در اینجا می‌توانیم یک fallback incident ایجاد کنیم یا alert بفرستیم
            // اما exception را throw نمی‌کنیم تا event handler crash نکند
        }
    }
}