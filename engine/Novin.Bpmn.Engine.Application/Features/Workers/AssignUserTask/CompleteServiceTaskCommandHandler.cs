using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;

public sealed class CompleteUserTaskCommandHandler
    : IRequestHandler<CompleteUserTaskCommand, CompleteUserTaskResult>
{
    private readonly IUnitOfWork _uow;
    private readonly IBpmnRuntimeContextFactory _ctxFactory;
    private readonly IVariableMappingService _variableMapping;
    private readonly ILogger<CompleteUserTaskCommandHandler> _logger;

    public CompleteUserTaskCommandHandler(
        IUnitOfWork uow,
        IBpmnRuntimeContextFactory ctxFactory,
        IVariableMappingService variableMapping,
        ILogger<CompleteUserTaskCommandHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _ctxFactory = ctxFactory ?? throw new ArgumentNullException(nameof(ctxFactory));
        _variableMapping = variableMapping ?? throw new ArgumentNullException(nameof(variableMapping));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CompleteUserTaskResult> Handle(CompleteUserTaskCommand cmd, CancellationToken ct)
    {
        if (cmd is null) throw new ArgumentNullException(nameof(cmd));
        if (cmd.WorkerId == Guid.Empty) return CompleteUserTaskResult.NotFound;

        CompleteUserTaskResult result = CompleteUserTaskResult.Ok;

        await _uow.ExecuteInTransactionAsync(async trxCt =>
        {
            var task = await _uow.UserTaskInstances.GetByIdAsync(cmd.WorkerId, trxCt);
            if (task is null)
            {
                result = CompleteUserTaskResult.NotFound;
                return;
            }

            // Terminal safety / idempotency
            if (task.Status == UserTaskStatus.Completed)
            {
                result = CompleteUserTaskResult.Ok;
                return;
            }

            if (task.Status == UserTaskStatus.Canceled)
            {
                result = CompleteUserTaskResult.InvalidState;
                return;
            }

            var token = await _uow.Tokens.GetByIdAsync(task.TokenId, trxCt);
            if (token is null)
            {
                result = CompleteUserTaskResult.TokenNotWaiting;
                return;
            }

            // Correlation: token must be waiting on THIS task
            if (token.State != TokenState.Waiting)
            {
                result = CompleteUserTaskResult.TokenNotWaiting;
                return;
            }

            var process = await _uow.Processes.GetByIdAsync(task.ProcessId, trxCt);
            if (process is null)
            {
                _logger.LogError("Process {ProcessId} not found for user task {TaskId}", task.ProcessId, task.Id);
                result = CompleteUserTaskResult.InvalidState;
                return;
            }

            // Ensure task state allows completion:
            // if you require InProgress only => enforce it.
            // Otherwise allow Ready/Claimed -> Start -> Complete.
            if (task.Status is UserTaskStatus.Ready or UserTaskStatus.Claimed)
                task.Start(cmd.CompletedBy);

            if (task.Status != UserTaskStatus.InProgress)
            {
                result = CompleteUserTaskResult.InvalidState;
                return;
            }

            task.Complete(cmd.CompletedBy, cmd.Result);

            // ✅ Set result variables on token (UserTask outputs are token-level)
            if (cmd.Result != null && cmd.Result.Count > 0)
            {
                foreach (var kvp in cmd.Result)
                {
                    token.SetVariable(kvp.Key, kvp.Value);
                }

                _logger.LogInformation("Set {VariableCount} result variables on token {TokenId}",
                    cmd.Result.Count, token.Id);

                // ✅ Apply output mapping: Token → Process (at activity execution boundary)
                try
                {
                    var ctx = await _ctxFactory.CreateAsync(process, trxCt);
                    var element = ctx.Model?.GetElementById(ctx.BpmnProcessId, task.ElementId);
                    if (element != null)
                    {
                        _variableMapping.ApplyOutputs(process, token, element, ctx);
                        _logger.LogInformation("Applied output mapping for user task {ElementId} on process {ProcessId}",
                            task.ElementId, process.Id);
                    }
                    else
                    {
                        _logger.LogWarning("Could not find BPMN element {ElementId} for output mapping", task.ElementId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error applying output mapping for user task {ElementId}", task.ElementId);
                    // Continue with token resumption even if mapping fails
                }
            }

            // Release token so engine can navigate
            token.Resume();
            token.Processed();

            await _uow.UserTaskInstances.UpdateAsync(task, trxCt);
            await _uow.Tokens.UpdateAsync(token, trxCt);
            await _uow.Processes.UpdateAsync(process, trxCt);
        }, ct);

        return result;
    }
}
