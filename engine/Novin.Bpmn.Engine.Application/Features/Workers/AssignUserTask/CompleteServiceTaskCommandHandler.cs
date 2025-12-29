using MediatR;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;

public sealed class CompleteUserTaskCommandHandler
    : IRequestHandler<CompleteUserTaskCommand, CompleteUserTaskResult>
{
    private readonly IUnitOfWork _uow;

    public CompleteUserTaskCommandHandler(IUnitOfWork uow)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
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

            // Release token so engine can navigate
            token.Resume();
            token.Processed();

            await _uow.UserTaskInstances.UpdateAsync(task, trxCt);
            await _uow.Tokens.UpdateAsync(token, trxCt);

            await _uow.CommitTransactionAsync(trxCt);
        }, ct);

        return result;
    }
}
