using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Models.Models;

public sealed class UserTaskHandler : IBpmnElementHandler
{
    private readonly IUserTaskService _userTaskService;
    private readonly ITokenNavigationService _nav;

    public UserTaskHandler(IUserTaskService userTaskService, ITokenNavigationService nav)
    {
        _userTaskService = userTaskService ?? throw new ArgumentNullException(nameof(userTaskService));
        _nav = nav ?? throw new ArgumentNullException(nameof(nav));
    }

    public bool CanHandle(BpmnFlowElement element) => element is BpmnUserTask;

    public Task HandleAsync(Process process, Token token, BpmnFlowElement element, BpmnRuntimeContext ctx, CancellationToken ct)
    {
        // ✅ Token-Centric Model: Trace tokens never participate in task execution
        // Trace tokens bypass UserTask and continue navigation
        if (!token.IsExecutable)
        {
            // Trace token => bypass task and navigate to next element
            return _nav.MoveNextOrForkAsync(process, token, ctx, executableMode: false, ct);
        }

        return _userTaskService.CreateAndWaitAsync(process, token, (BpmnUserTask)element, ct);
    }
}