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

    public override Task<ElementProcessResult> ProcessAsync(
        Process process,
        Token token,
        NodeInstance node,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct)
    {
        if (token.State is TokenState.Completed
            or TokenState.Terminated
            or TokenState.Failed)
            return Task.FromResult(ElementProcessResult.NoOp);

        if (token.IsExecutable && !isResume)
        {
            token.ClearLocalVariables();
            _mapping.ApplyInputs(process, token, element, ctx);
        }

        if (token.IsExecutable)
            _mapping.ApplyOutputs(process, token, element, ctx);

        token.Processed();
        node.Complete();
        return Task.FromResult(ElementProcessResult.Completed);
    }
}