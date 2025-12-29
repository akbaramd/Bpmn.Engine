using MediatR;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;

public sealed class FailServiceTaskCommandHandler
    : IRequestHandler<FailServiceTaskCommand, FailServiceTaskResult>
{
    private readonly IUnitOfWork _uow;

    public FailServiceTaskCommandHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<FailServiceTaskResult> Handle(
        FailServiceTaskCommand cmd,
        CancellationToken ct)
    {
        await _uow.BeginTransactionAsync(ct);
        var worker = await _uow.Workers.GetByIdAsync(cmd.WorkerId, ct);
        if (worker == null)
            return FailServiceTaskResult.NotFound;

        if (worker.Type != WorkerType.ServiceTask)
            return FailServiceTaskResult.InvalidState;

        var token = await _uow.Tokens.GetByIdAsync(worker.TokenId, ct);
        if (token == null ||
            token.State is TokenState.Completed or TokenState.Terminated)
            return FailServiceTaskResult.InvalidState;

        worker.MarkFailed(cmd.ErrorMessage, cmd.FailedByClientId);

        token.Fail(
            error: cmd.ErrorMessage,
            errorType: ErrorType.TechnicalFailure,
            errorCode: cmd.ErrorCode);

        await _uow.Workers.UpdateAsync(worker, ct);
        await _uow.Tokens.UpdateAsync(token, ct);
        await _uow.CommitTransactionAsync(ct);

        return FailServiceTaskResult.Ok;
    }
}
