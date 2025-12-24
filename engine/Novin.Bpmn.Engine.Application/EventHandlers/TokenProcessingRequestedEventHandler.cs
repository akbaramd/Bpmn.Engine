using MediatR;

public sealed class TokenProcessingRequestedEventHandler
    : INotificationHandler<TokenProcessingRequestedEvent>
{
    private readonly ITokenProcessingOrchestrator _orchestrator;

    public TokenProcessingRequestedEventHandler(ITokenProcessingOrchestrator orchestrator)
        => _orchestrator = orchestrator;

    public Task Handle(TokenProcessingRequestedEvent n, CancellationToken ct)
        => _orchestrator.ProcessAsync(n.ProcessId, n.TokenId, ct);
}