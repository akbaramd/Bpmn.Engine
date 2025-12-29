using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Commands.CreateToken;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Models.Models;

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
        Func<BpmnSequenceFlow, bool> isExecutableForFlow,
        BpmnRuntimeContext ctx,
        CancellationToken ct)
    {
        if (process is null) throw new ArgumentNullException(nameof(process));
        if (parent is null) throw new ArgumentNullException(nameof(parent));
        if (outgoing is null) throw new ArgumentNullException(nameof(outgoing));
        if (ctx is null) throw new ArgumentNullException(nameof(ctx));

        foreach (var flow in outgoing)
        {
            if (string.IsNullOrWhiteSpace(flow.targetRef))
                throw new InvalidOperationException("SequenceFlow targetRef is null/empty.");

            // 1) Validate target exists
            var targetElement = ctx.Model.GetElementById(ctx.BpmnProcessId, flow.targetRef);
            if (targetElement == null)
                throw new InvalidOperationException(
                    $"Target element '{flow.targetRef}' not found for flow '{FlowKey(flow)}'.");

            // 2) Determine if child token should be executable
            var isExecutable = isExecutableForFlow(flow);

            // 3) Prepare variables dictionary from parent (shallow copy)
            // توجه: Variable Mapping (ApplyInputs) توسط VariableMappingDecorator
            // هنگام اجرای child token انجام می‌شود، نه اینجا (SRP).
            var variables = parent.Variables.ToDictionary(kv => kv.Key, kv => kv.Value);

            // 4) Create child token using CreateTokenCommand
            // TokenCreatedEvent will automatically activate the token
            var createResult = await _mediator.Send(new CreateTokenCommand(
                ProcessId: process.Id,
                StartElementId: flow.targetRef,
                ParentTokenIds: new[] { parent.Id },
                ArrivedViaFlowId: FlowKey(flow),
                IsExecutable: isExecutable,
                ScopeId: scopeId,
                Variables: variables), ct);

            if (!createResult.Success)
            {
                _logger.LogError(
                    "[FORK] Failed to create child token. ProcessId={ProcessId} TargetElementId={TargetElementId} Error={Error}",
                    process.Id, flow.targetRef, createResult.Error);
                throw new InvalidOperationException(
                    $"Failed to create child token for flow '{FlowKey(flow)}': {createResult.Error}");
            }

            _logger.LogDebug(
                "[FORK] Created child token. TokenId={TokenId} ElementId={ElementId} IsExecutable={IsExecutable} ScopeId={ScopeId}",
                createResult.TokenId, flow.targetRef, isExecutable, scopeId);
        }
    }

    private static string FlowKey(BpmnSequenceFlow f)
        => !string.IsNullOrWhiteSpace(f.id) ? f.id! : $"{f.sourceRef}->{f.targetRef}";
}