using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.EventHandlers;


public sealed class ScriptTaskHandler : IBpmnElementHandler
{
    private readonly IScriptTaskExecutor _executor;
    private readonly ITokenNavigationService _nav;

    public ScriptTaskHandler(IScriptTaskExecutor executor, ITokenNavigationService nav)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _nav = nav ?? throw new ArgumentNullException(nameof(nav));
    }

    public bool CanHandle(BpmnFlowElement element) => element is BpmnScriptTask;

    public async Task HandleAsync(Process process, Token token, BpmnFlowElement element, BpmnRuntimeContext ctx, CancellationToken ct)
    {
        var st = (BpmnScriptTask)element;

        // bypass token => just pass through
        if (!token.IsExecutable)
        {
            await _nav.MoveNextOrForkAsync(process, token, ctx, executableMode: false, ct);
            return;
        }

        await _executor.ExecuteAsync(process, token, st, ct);

        if (token.State == TokenState.Failed) return;

        await _nav.MoveNextOrForkAsync(process, token, ctx, executableMode: true, ct);
    }
}

// -------- ServiceTask --------
public sealed class ServiceTaskHandler : IBpmnElementHandler
{
    private readonly IServiceTaskExecutor _executor;
    private readonly ITokenNavigationService _nav;

    public ServiceTaskHandler(IServiceTaskExecutor executor, ITokenNavigationService nav)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _nav = nav ?? throw new ArgumentNullException(nameof(nav));
    }

    public bool CanHandle(BpmnFlowElement element) => element is BpmnServiceTask;

    public async Task HandleAsync(Process process, Token token, BpmnFlowElement element, BpmnRuntimeContext ctx, CancellationToken ct)
    {
        var task = (BpmnServiceTask) element;

        // bypass token => just pass through
        if (!token.IsExecutable)
        {
            await _nav.MoveNextOrForkAsync(process, token, ctx, executableMode: false, ct);
            return;
        }

        await _executor.ExecuteAsync(process, token, task, ct);

        if (token.State == TokenState.Failed) return;

        await _nav.MoveNextOrForkAsync(process, token, ctx, executableMode: true, ct);
    }
}