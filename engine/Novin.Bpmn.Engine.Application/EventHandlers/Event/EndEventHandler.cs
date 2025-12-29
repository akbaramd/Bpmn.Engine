using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Commands.TerminateProcess;
using Novin.Bpmn.Engine.Application.Commands.TerminateToken;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models; 
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

public sealed class EndEventHandler : BpmnElementHandlerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<EndEventHandler> _logger;

    public EndEventHandler(
        IMediator mediator,
        IFeelExpressionEvaluator feel,
        ILogger<EndEventHandler> logger)
        : base(mediator, feel, logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override bool CanHandle(BpmnFlowElement element) => element is BpmnEndEvent;

    public override async Task<ElementProcessResult> ProcessAsync(
        Domain.Entities.Process process,
        Token token,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct)
    {
        if (process is null) throw new ArgumentNullException(nameof(process));
        if (token is null) throw new ArgumentNullException(nameof(token));
        if (element is null) throw new ArgumentNullException(nameof(element));

        var endEvent = (BpmnEndEvent)element;
        var isTerminate = IsTerminateEndEvent(endEvent);

        using (_logger.BeginScope(new Dictionary<string, string?>
               {
                   ["ProcessId"] = process.Id.ToString(),
                   ["TokenId"] = token.Id.ToString(),
                   ["ElementId"] = token.CurrentElementId,
                   ["Executable"] = token.IsExecutable.ToString(),
                   ["TokenState"] = token.State.ToString(),
                   ["IsTerminateEnd"] = isTerminate.ToString()
               }))
        {
            _logger.LogInformation(
                "[END] ProcessAsync. Terminate={Terminate} State={State} Exec={Exec} Resume={Resume}",
                isTerminate, token.State, token.IsExecutable, isResume);

            // Defensive: EndEvent should normally be processed while Active
            if (token.State is TokenState.Terminated or TokenState.Completed or TokenState.Failed)
            {
                _logger.LogWarning("[END] Ignored: token already ended. State={State}", token.State);
                return ElementProcessResult.NoOp;
            }

            if (isTerminate)
            {
                // ✅ Terminate End => terminate whole process instance (ALL live tokens, including trace)
                await _mediator.Send(new TerminateProcessCommand(
                   process.Id,
                   $"Terminate End Event reached at '{endEvent.id}'"), ct);

                return ElementProcessResult.Completed;
            }
            
            

            // Normal End: end فقط همین توکن (Terminal state)
            if (token.IsExecutable)
            {
                // ✅ Terminal state: use Complete() directly
                token.Complete();
            }
            else
            {
                token.Terminate();
            }

            return ElementProcessResult.Completed;
        }
    }

    // EndEvent => no navigation
    public override System.Threading.Tasks.Task NavigateAsync(
        Domain.Entities.Process process,
        Token token,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct)
        => System.Threading.Tasks.Task.CompletedTask;

    private static bool IsTerminateEndEvent(BpmnEndEvent endEvent)
    {
        var items = endEvent.Items;
        if (items == null || items.Length == 0) return false;
        return items.Any(x => x is BpmnTerminateEventDefinition);
    }
}
