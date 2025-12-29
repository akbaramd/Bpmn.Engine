using MediatR;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Repositories;

public sealed class AssignUserTaskCommandHandler
    : IRequestHandler<AssignUserTaskCommand, AssignUserTaskResult>
{
    private readonly IUnitOfWork _uow;
    private readonly IUserTaskInstanceRepository _tasks;

    public AssignUserTaskCommandHandler(IUnitOfWork uow, IUserTaskInstanceRepository tasks)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
    }

    public async Task<AssignUserTaskResult> Handle(AssignUserTaskCommand cmd, CancellationToken ct)
    {
        if (cmd is null) throw new ArgumentNullException(nameof(cmd));
        if (cmd.WorkerId == Guid.Empty) return AssignUserTaskResult.NotFound;

        AssignUserTaskResult result = AssignUserTaskResult.Ok;

        await _uow.ExecuteInTransactionAsync(async trxCt =>
        {
            var task = await _tasks.GetByIdAsync(cmd.WorkerId, trxCt);
            if (task is null)
            {
                result = AssignUserTaskResult.NotFound;
                return;
            }

            // Only assign while not terminal
            // (Your aggregate will throw if terminal, but we prefer explicit result)
            if (task.Status is UserTaskStatus.Completed or UserTaskStatus.Canceled)
            {
                result = AssignUserTaskResult.InvalidState;
                return;
            }

            // ----- Apply assignment metadata (domain contract) -----

            if (!string.IsNullOrWhiteSpace(cmd.Assignee))
                task.SetMeta(UserTaskMeta.Assignee, cmd.Assignee);

            if (!string.IsNullOrWhiteSpace(cmd.CandidateGroups))
                task.SetMeta(UserTaskMeta.CandidateGroups, cmd.CandidateGroups);

            if (!string.IsNullOrWhiteSpace(cmd.CandidateUsers))
                task.SetMeta(UserTaskMeta.CandidateUsers, cmd.CandidateUsers);

            if (cmd.Priority.HasValue)
                task.SetMeta(UserTaskMeta.Priority, cmd.Priority.Value);

            if (cmd.DueDateUtc.HasValue)
                task.SetMeta(UserTaskMeta.DueDateUtc, cmd.DueDateUtc.Value.ToString("O"));

            // audit (keep them as custom metadata keys)
            if (!string.IsNullOrWhiteSpace(cmd.AssignedBy))
                task.SetMeta("assignedBy", cmd.AssignedBy);

            task.SetMeta("assignedAtUtc", DateTime.UtcNow.ToString("O"));

            await _tasks.UpdateAsync(task, trxCt);

            // persist
            await _uow.CommitTransactionAsync(trxCt);
        }, ct);

        return result;
    }
}
