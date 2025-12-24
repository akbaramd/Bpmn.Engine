using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Models.Models;

public interface ITokenNavigationService
{
    Task MoveNextOrForkAsync(Process process, Token token, BpmnRuntimeContext ctx, bool executableMode, CancellationToken ct);
}

public sealed class DefaultFlowNodeHandler : IBpmnElementHandler
{
    private readonly ITokenNavigationService _nav;

    public DefaultFlowNodeHandler(ITokenNavigationService nav) => _nav = nav;

    public bool CanHandle(BpmnFlowElement element)
        => element is BpmnFlowNode
           && element is not BpmnGateway
           && element is not BpmnStartEvent
           && element is not BpmnEndEvent
           && element is not BpmnScriptTask
           && element is not BpmnServiceTask
           && element is not BpmnUserTask;

    public Task HandleAsync(Process process, Token token, BpmnFlowElement element, BpmnRuntimeContext ctx, CancellationToken ct)
        => _nav.MoveNextOrForkAsync(process, token, ctx, executableMode: token.IsExecutable, ct);
}


public sealed class TokenNavigationService : ITokenNavigationService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<TokenNavigationService> _logger;

    public TokenNavigationService(IUnitOfWork uow, ILogger<TokenNavigationService> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task MoveNextOrForkAsync(
        Process process,
        Token token,
        BpmnRuntimeContext ctx,
        bool executableMode,
        CancellationToken ct)
    {
        if (process == null) throw new ArgumentNullException(nameof(process));
        if (token == null) throw new ArgumentNullException(nameof(token));
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));

        var outgoing = ctx.Model.GetOutgoingSequenceFlows(ctx.BpmnProcessId, token.CurrentElementId);

        // No outgoing => token ends here (engine-safe)
        if (outgoing.Count == 0)
        {
            _logger.LogDebug("Token {TokenId} has no outgoing from {ElementId}. Terminating.", token.Id, token.CurrentElementId);
            token.Terminate("No outgoing sequence flow.");
            process.RemoveToken(token.Id);
            return;
        }

        // Single outgoing => simple move
        if (outgoing.Count == 1)
        {
            var f = outgoing[0];
            if (string.IsNullOrWhiteSpace(f.targetRef))
                throw new InvalidOperationException("SequenceFlow targetRef is null/empty.");

            token.MoveTo(f.targetRef, FlowKey(f));
            return;
        }

        // Multiple outgoing => fork children
        var scopeId = token.ScopeId ?? Guid.NewGuid();

        // Terminate current token and remove from process (matches your current behavior)
        token.Terminate("Fork to multiple outgoing.");
        process.RemoveToken(token.Id);

        foreach (var f in outgoing)
        {
            if (string.IsNullOrWhiteSpace(f.targetRef))
                throw new InvalidOperationException("SequenceFlow targetRef is null/empty.");

            var child = new Token(process.Id, f.targetRef, new[] { token.Id });
            child.SetScope(scopeId);
            foreach (var kv in token.Variables)
                child.SetVariable(kv.Key, kv.Value);
            if (!executableMode)
                child.MarkNonExecutable("Bypass propagation.");

            child.SetArrivedVia(FlowKey(f));   // ✅ قبل از Activate
            child.Activate();                  // ✅ حالا RequestProcessing شامل ArrivedVia است

            await _uow.Tokens.AddAsync(child, ct);
            process.AddToken(child.Id);
        }
    }

    private static string FlowKey(BpmnSequenceFlow f)
        => !string.IsNullOrWhiteSpace(f.id) ? f.id! : $"{f.sourceRef}->{f.targetRef}";
}