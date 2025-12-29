using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Commands.WaitToken;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

public sealed class UserTaskHandler : BpmnElementHandlerBase
{
    private readonly IUserTaskService _userTaskService;
    private readonly IVariableMappingService _variableMapping;
    private readonly ILogger<UserTaskHandler> _logger;

    public UserTaskHandler(
        IUserTaskService userTaskService,
        IVariableMappingService variableMapping,
        IMediator mediator,
        IFeelExpressionEvaluator feel,
        ILogger<UserTaskHandler> logger)
        : base(mediator, feel, logger)
    {
        _userTaskService = userTaskService ?? throw new ArgumentNullException(nameof(userTaskService));
        _variableMapping = variableMapping ?? throw new ArgumentNullException(nameof(variableMapping));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override bool CanHandle(BpmnFlowElement element) => element is BpmnUserTask;

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
        if (ctx is null) throw new ArgumentNullException(nameof(ctx));

        var userTask = (BpmnUserTask)element;

        using (_logger.BeginScope(new Dictionary<string, string?>
               {
                   ["ProcessId"] = process.Id.ToString(),
                   ["TokenId"] = token.Id.ToString(),
                   ["ElementId"] = token.CurrentElementId,
                   ["Executable"] = token.IsExecutable.ToString(),
                   ["IsResume"] = isResume.ToString()
               }))
        {
            _logger.LogInformation(
                "[USER-TASK] ProcessAsync. Exec={Exec} Resume={Resume} State={State} ElementId={ElementId}",
                token.IsExecutable, isResume, token.State, token.CurrentElementId);

            // اگر توکن قبلا ended شده، هیچ کاری نکن (idempotent/safety)
            if (token.State is TokenState.Completed or TokenState.Terminated or TokenState.Failed)
            {
                _logger.LogWarning("[USER-TASK] Ignored: token already ended. State={State}", token.State);
                return ElementProcessResult.NoOp;
            }

            // 1) Input Mapping فقط برای executable و فقط بار اول
            if (token.IsExecutable && !isResume)
            {
                token.ClearLocalVariables();
                _variableMapping.ApplyInputs(process, token, element, ctx);
                _logger.LogDebug("[USER-TASK] Input mapping applied.");
            }

            // 2) Trace token: هیچ UserTask نساز، فقط اجازه بده Navigate انجام شود
            if (!token.IsExecutable)
            {
                _logger.LogDebug("[USER-TASK] Trace token => skip CreateAndWait. Will navigate.");
                token.Processed();
                return ElementProcessResult.Waiting;
            }

            // 3) Resume: یعنی UserTask قبلا complete شده و token دوباره active شده
            // در این مرحله فقط باید Navigate کنیم. (OutputMapping را بهتر است در handler تکمیل UserTask انجام دهید)
            if (isResume)
            {
                _logger.LogInformation("[USER-TASK] Resume => navigate only.");
                token.Processed();
                return ElementProcessResult.Waiting;
            }

            // 4) Create user task + wait (توکن باید Waiting شود)
            _logger.LogInformation("[USER-TASK] Creating user task and waiting. UserTaskId={UserTaskId} Name={Name}",
                userTask.id, userTask.name);

           var workerId =  await _userTaskService.CreateAsync(process, token, userTask, ct);

           if (workerId == Guid.Empty)
           {     return ElementProcessResult.NoOp;
               
           }

            await Mediator.Send(new WaitTokenCommand(
                ProcessId: process.Id,
                TokenId: token.Id,
                Reason: $"Waiting for service task: {userTask.name}",
                WorkerId: workerId), ct);
            return ElementProcessResult.Waiting;
        }
    }

    // برای UserTask، Navigate استاندارد base کافی است:
    // - اگر Waiting/Terminated/Failed باشد حرکت نمی‌کند
    // - اگر outgoing چندتا باشد با FEEL انتخاب می‌کند
    public override System.Threading.Tasks.Task NavigateAsync(
        Domain.Entities.Process process,
        Token token,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct)
        => base.NavigateAsync(process, token, element, ctx, isResume, ct);
}
