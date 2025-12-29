using MediatR;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;

public sealed class AssignUserTaskCommandHandler
    : IRequestHandler<AssignUserTaskCommand, AssignUserTaskResult>
{
    private readonly IUnitOfWork _uow;

    public AssignUserTaskCommandHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<AssignUserTaskResult> Handle(
        AssignUserTaskCommand cmd,
        CancellationToken ct)
    {
          await _uow.BeginTransactionAsync(ct);
        var worker = await _uow.Workers.GetByIdAsync(cmd.WorkerId, ct);
        if (worker == null)
            return AssignUserTaskResult.NotFound;

        if (worker.Type != WorkerType.UserTask ||
            worker.Status != WorkerStatus.Pending)
            return AssignUserTaskResult.InvalidState;

        if (!string.IsNullOrWhiteSpace(cmd.Assignee))
            worker.SetMeta("assignee", cmd.Assignee);

        if (!string.IsNullOrWhiteSpace(cmd.CandidateGroups))
            worker.SetMeta("candidateGroups", cmd.CandidateGroups);

        if (cmd.Priority.HasValue)
            worker.SetMeta("priority", cmd.Priority.Value);

        if (cmd.DueDateUtc.HasValue)
            worker.SetMeta("dueDate", cmd.DueDateUtc.Value.ToString("O"));

        worker.SetMeta("assignedBy", cmd.AssignedBy);
        worker.SetMeta("assignedAt", DateTime.UtcNow.ToString("O"));

        await _uow.Workers.UpdateAsync(worker, ct);
        await _uow.CommitTransactionAsync(ct);

        return AssignUserTaskResult.Ok;
    }
}
