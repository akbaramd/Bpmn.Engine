using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Models.Models;

public sealed class UserTaskHandler : IBpmnElementHandler
{
    private readonly IUserTaskService _userTaskService;

    public UserTaskHandler(IUserTaskService userTaskService) => _userTaskService = userTaskService;

    public bool CanHandle(BpmnFlowElement element) => element is BpmnUserTask;

    public Task HandleAsync(Process process, Token token, BpmnFlowElement element, BpmnRuntimeContext ctx, CancellationToken ct)
    {
        if (!token.IsExecutable)
            return Task.CompletedTask; // یا: navigation service برای bypass

        return _userTaskService.CreateAndWaitAsync(process, token, (BpmnUserTask)element, ct);
    }
}