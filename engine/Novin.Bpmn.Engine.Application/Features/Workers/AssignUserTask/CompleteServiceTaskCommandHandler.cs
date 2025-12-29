using MediatR;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;

public sealed class CompleteServiceTaskCommandHandler
    : IRequestHandler<CompleteServiceTaskCommand, CompleteServiceTaskResult>
{
    private readonly IUnitOfWork _uow;

    public CompleteServiceTaskCommandHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<CompleteServiceTaskResult> Handle(
        CompleteServiceTaskCommand cmd,
        CancellationToken ct)
    {
        
        await _uow.BeginTransactionAsync(ct);
        var worker = await _uow.Workers.GetByIdAsync(cmd.WorkerId, ct);
        if (worker == null)
            return CompleteServiceTaskResult.NotFound;

        if (worker.Type != WorkerType.ServiceTask)
            return CompleteServiceTaskResult.InvalidState;

        var token = await _uow.Tokens.GetByIdAsync(worker.TokenId, ct);
        if (token == null ||
            token.State != TokenState.Waiting ||
            token.WorkerId != worker.Id)
            return CompleteServiceTaskResult.TokenNotWaiting;

        if (worker.Status == WorkerStatus.Pending)
            worker.MarkStarted(cmd.CompletedByClientId);

        worker.MarkCompleted(cmd.CompletedByClientId, cmd.Result);

        token.Resume();
        token.Processed();

        await _uow.Workers.UpdateAsync(worker, ct);
        await _uow.Tokens.UpdateAsync(token, ct);
        await _uow.CommitTransactionAsync(ct);

        return CompleteServiceTaskResult.Ok;
    }
}
