using Microsoft.Extensions.Logging;
using MediatR;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

/// <summary>
/// Handles simple flow nodes (IntermediateCatchEvent, ThrowEvent, etc.)
/// </summary>
public sealed class DefaultFlowNodeHandler : BpmnElementHandlerBase
{
    private readonly IVariableMappingService _mapping;

    public DefaultFlowNodeHandler(
        IMediator mediator,
        IFeelExpressionEvaluator feel,
        IVariableMappingService mapping,
        ILogger<DefaultFlowNodeHandler> logger)
        : base(mediator, feel, logger)
    {
        _mapping = mapping;
    }

    public override bool CanHandle(BpmnFlowElement element)
        => element is BpmnFlowNode
           && element is not BpmnGateway
           && element is not BpmnStartEvent
           && element is not BpmnEndEvent
           && element is not BpmnScriptTask
           && element is not BpmnServiceTask
           && element is not BpmnUserTask;

    public override async Task<ElementProcessResult> ProcessAsync(
        Process process,
        Token token,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct)
    {
        // Guard: don't process if token is already in terminal state
        if (token.State is Domain.ValueObjects.TokenState.Completed 
            or Domain.ValueObjects.TokenState.Terminated 
            or Domain.ValueObjects.TokenState.Failed)
        {
            Logger.LogDebug("[DEFAULT] Token already in terminal state. Element={ElementId} State={State}", 
                element.id, token.State);
            return ElementProcessResult.NoOp;
        }

        if (token.IsExecutable && !isResume)
        {
            token.ClearLocalVariables();
            _mapping.ApplyInputs(process, token, element, ctx);
            Logger.LogDebug("[DEFAULT] Input mapping done. Element={ElementId}", element.id);
        }

        if (token.IsExecutable)
        {
            _mapping.ApplyOutputs(process, token, element, ctx);
            Logger.LogDebug("[DEFAULT] Output mapping done. Element={ElementId}", element.id);
        }

        // ✅ IMPORTANT: Mark token as processed (NodeDone)
        // This publishes TokenProcessedEvent which triggers navigation
        token.Processed();

        Logger.LogDebug("[DEFAULT] Token processed. Element={ElementId} TokenId={TokenId}", 
            element.id, token.Id);

        return ElementProcessResult.Completed; // element done, ready for navigation
    }
}