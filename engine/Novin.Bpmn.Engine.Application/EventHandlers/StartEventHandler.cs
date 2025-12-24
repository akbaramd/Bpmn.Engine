using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Execution.Strategies;

public sealed class StartEventHandler : IBpmnElementHandler
{
    private readonly ITokenNavigationService _nav;

    public StartEventHandler(ITokenNavigationService nav)
        => _nav = nav ?? throw new ArgumentNullException(nameof(nav));

    public bool CanHandle(BpmnFlowElement element) => element is BpmnStartEvent;

    public Task HandleAsync(Process process, Token token, BpmnFlowElement element, BpmnRuntimeContext ctx, CancellationToken ct)
    {
        // StartEvent (بدون eventDefinition) => straight-through
        // اگر token bypass هم باشد، صرفاً عبور کند
        return _nav.MoveNextOrForkAsync(process, token, ctx, executableMode: token.IsExecutable, ct);
    }
}