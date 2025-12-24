using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Models.Models;

public interface IUserTaskService
{
    Task CreateAndWaitAsync(Process process, Token token, BpmnUserTask ut, CancellationToken ct);
}

public sealed class UserTaskService : IUserTaskService
{
    private readonly IUnitOfWork _uow;

    public UserTaskService(IUnitOfWork uow)
        => _uow = uow ?? throw new ArgumentNullException(nameof(uow));

    public async Task CreateAndWaitAsync(Process process, Token token, BpmnUserTask ut, CancellationToken ct)
    {
        if (process == null) throw new ArgumentNullException(nameof(process));
        if (token == null) throw new ArgumentNullException(nameof(token));
        if (ut == null) throw new ArgumentNullException(nameof(ut));

        if (!token.IsExecutable)
        {
            // bypass token should never create tasks
            token.Wait("Bypass token reached UserTask (ignored).");
            return;
        }

        if (string.IsNullOrWhiteSpace(ut.id))
            throw new InvalidOperationException("UserTask BPMN id is null/empty.");

        var userTask = new UserTask(process.Id, ut.name ?? "User Task", ut.id!);
        await _uow.Tasks.AddAsync(userTask, ct);

        token.Wait("Waiting for user task completion.");
    }
}