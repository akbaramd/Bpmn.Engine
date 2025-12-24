using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.ValueObjects;

public interface ITokenProcessingOrchestrator
{
    Task ProcessAsync(Guid processId, Guid tokenId, CancellationToken ct);
}

public sealed class TokenProcessingOrchestrator : ITokenProcessingOrchestrator
{
    private readonly IUnitOfWork _uow;
    private readonly IBpmnRuntimeContextFactory _ctxFactory;
    private readonly ITokenExecutionDispatcher _dispatcher;
    private readonly ILogger<TokenProcessingOrchestrator> _logger;

    public TokenProcessingOrchestrator(
        IUnitOfWork uow,
        IBpmnRuntimeContextFactory ctxFactory,
        ITokenExecutionDispatcher dispatcher,
        ILogger<TokenProcessingOrchestrator> logger)
    {
        _uow = uow;
        _ctxFactory = ctxFactory;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public Task ProcessAsync(Guid processId, Guid tokenId, CancellationToken ct)
        => _uow.ExecuteInTransactionAsync(async trxCt =>
        {
            var process = await _uow.Processes.GetByIdAsync(processId, trxCt);
            if (process == null) { _logger.LogWarning("Process not found {Id}", processId); return; }

            var token = await _uow.Tokens.GetByIdAsync(tokenId, trxCt);
            if (token == null) { _logger.LogWarning("Token not found {Id}", tokenId); return; }

            if (token.State != TokenState.Active) return;

            var ctx = await _ctxFactory.CreateAsync(process, trxCt);

            var element = ctx.Model.GetElementById(ctx.BpmnProcessId, token.CurrentElementId);
            if (element == null) { token.Fail($"Element '{token.CurrentElementId}' not found."); return; }

            using (_logger.BeginScope(new Dictionary<string, object?>
            {
                ["ProcessId"] = process.Id,
                ["TokenId"] = token.Id,
                ["ElementId"] = token.CurrentElementId,
                ["ScopeId"] = token.ScopeId,
                ["ArrivedVia"] = token.ArrivedViaFlowId,
                ["Executable"] = token.IsExecutable,
                ["State"] = token.State.ToString()
            }))
            {
                await _dispatcher.DispatchAsync(process, token, element, ctx, trxCt);
            }
        }, ct);
}