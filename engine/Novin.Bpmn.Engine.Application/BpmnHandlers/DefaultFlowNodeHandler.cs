using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;

public sealed class DefaultFlowNodeHandler : BpmnElementHandlerBase
{
    private readonly IVariableMappingService _mapping;

    public DefaultFlowNodeHandler(
        IFeelExpressionEvaluator feel,
        IVariableMappingService mapping,
        ILogger<DefaultFlowNodeHandler> logger)
        : base(feel, logger)
    {
        _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
    }

    public override bool CanHandle(BpmnFlowElement element)
        => element is BpmnFlowNode
           && element is not BpmnGateway
           && element is not BpmnStartEvent
           && element is not BpmnEndEvent
           && element is not BpmnScriptTask
           && element is not BpmnServiceTask
           && element is not BpmnUserTask;

    // ✅ Gate/Permission level (اختیاری ولی استاندارد)
    public override Task<TokenProcessResult> TokenProcessAsync(
        Process process,
        Token token,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct)
    {
        if (token.State is TokenState.Terminated or TokenState.Failed)
            return Task.FromResult(TokenProcessResult.NoOp);

        if (token.State == TokenState.Waiting)
            return Task.FromResult(TokenProcessResult.Waiting);

        return Task.FromResult(TokenProcessResult.Continue);
    }

    public override Task<ElementProcessResult> NodeProcessAsync(
        Process process,
        Token token,
        NodeInstance node,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // Terminal token => no-op
        if (token.State is TokenState.Terminated or TokenState.Failed)
            return Task.FromResult(ElementProcessResult.NoOp);

        // ✅ Node lifecycle
        node.Start();

        // ✅ Trace token => no mappings, mark node skipped (بهترین برای observability)
        if (!token.IsExecutable)
        {
            token.Processed();
            node.Skip("Trace token (non-executable)");
            return Task.FromResult(ElementProcessResult.Completed);
        }

        // ✅ Executable token => apply inputs once (first time only)
        if (!isResume)
        {
            token.ClearLocalVariables();
            _mapping.ApplyInputs(process, token, element, ctx);
        }

        // ✅ Output mapping: Token → Process (only when activity completes)
        // Note: DefaultFlowNodeHandler handles simple elements that always complete immediately
        // (no waiting), so we can safely apply outputs here
        if (token.IsExecutable)
            _mapping.ApplyOutputs(process, token, element, ctx);

        // ✅ Done
        token.Processed();
        node.Complete();

        return Task.FromResult(ElementProcessResult.Completed);
    }
}
