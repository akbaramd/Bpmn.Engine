using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Commands.CreateToken;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Models.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Novin.Bpmn.Engine.Application.Services;

public sealed class TokenForkService : ITokenForkService
{
    private readonly IMediator _mediator;
    private readonly ILogger<TokenForkService> _logger;

    public TokenForkService(
        IMediator mediator,
        ILogger<TokenForkService> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ForkChildrenAsync(
        Process process,
        Token parent,
        IReadOnlyList<BpmnSequenceFlow> outgoing,
        Guid scopeId,
        BpmnRuntimeContext ctx,
        CancellationToken ct)
    {
        if (process is null) throw new ArgumentNullException(nameof(process));
        if (parent is null) throw new ArgumentNullException(nameof(parent));
        if (outgoing is null) throw new ArgumentNullException(nameof(outgoing));
        if (ctx is null) throw new ArgumentNullException(nameof(ctx));

        // ---- critical validation: parent must already have scopeId on top-of-stack ----
        // If GatewaySplitService did token.SetScope(scopeId) before calling ForkChildrenAsync, this should pass.
        var parentStack = parent.ScopeStack?.ToArray() ?? Array.Empty<Guid>();
        if (parentStack.Length == 0 || parent.ScopeId != scopeId)
        {
            // fallback (still try to proceed safely)
            _logger.LogError(
                "[FORK:CFG] Parent scope stack mismatch BEFORE forking. " +
                "Proc={Proc} Parent={Parent} ExpectedScope={ExpectedScope} ParentScope={ParentScope} Depth={Depth}",
                process.Id, parent.Id, scopeId, parent.ScopeId, parentStack.Length);

            // Ensure a stable snapshot to send
            if (parentStack.Length == 0)
                parentStack = new[] { scopeId };
            else if (parent.ScopeId != scopeId)
                parentStack = parentStack.Concat(new[] { scopeId }).ToArray();
        }

        _logger.LogInformation(
            "[FORK] Begin. Proc={Proc} Parent={Parent} Scope={Scope} Depth={Depth} ForkCount={ForkCount}",
            process.Id, parent.Id, scopeId, parentStack.Length, outgoing.Count);

        for (var i = 0; i < outgoing.Count; i++)
        {
            var flow = outgoing[i];
            if (string.IsNullOrWhiteSpace(flow.targetRef))
                throw new InvalidOperationException("SequenceFlow targetRef is null/empty.");

            // Validate target exists
            var targetElement = ctx.Model.GetElementById(ctx.BpmnProcessId, flow.targetRef);
            if (targetElement == null)
                throw new InvalidOperationException(
                    $"Target element '{flow.targetRef}' not found for flow '{FlowKey(flow)}'.");

            // Clone variables from parent (deterministic snapshot for the child)
            var variables = parent.Variables.Count == 0
                ? null
                : parent.Variables.ToDictionary(kv => kv.Key, kv => kv.Value);

            // Create child token with:
            // - ParentTokenId = parent.Id
            // - ScopeStackSnapshot = parent.ScopeStack snapshot (includes new scope on top)
            var createResult = await _mediator.Send(
                new CreateTokenCommand(
                    ProcessId: process.Id,
                    StartElementId: flow.targetRef!,
                    ParentTokenId: parent.Id,
                    ArrivedViaFlowId: FlowKey(flow),
                    ScopeId: scopeId, // backward compatibility; handler prefers stack
                    Variables: variables,
                    ScopeStackSnapshot: parentStack
                ),
                ct);

            if (!createResult.Success)
            {
                _logger.LogError(
                    "[FORK:FAIL] Failed to create child token. Proc={Proc} Parent={Parent} Target={Target} Flow={Flow} Error={Error}",
                    process.Id, parent.Id, flow.targetRef, FlowKey(flow), createResult.Error);

                throw new InvalidOperationException(
                    $"Failed to create child token for flow '{FlowKey(flow)}': {createResult.Error}");
            }

            _logger.LogDebug(
                "[FORK] Child created. Proc={Proc} Parent={Parent} Child={Child} Target={Target} Scope={Scope} Depth={Depth} Flow={Flow}",
                process.Id, parent.Id, createResult.TokenId, flow.targetRef, scopeId, parentStack.Length, FlowKey(flow));
        }

        _logger.LogInformation(
            "[FORK] Done. Proc={Proc} Parent={Parent} Scope={Scope} Forked={Forked}",
            process.Id, parent.Id, scopeId, outgoing.Count);
    }

    private static string FlowKey(BpmnSequenceFlow f)
        => !string.IsNullOrWhiteSpace(f.id) ? f.id! : $"{f.sourceRef}->{f.targetRef}";
}
