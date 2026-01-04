using MediatR;
using Newtonsoft.Json;
using Novin.Bpmn.Engine.Domain.Repositories;

namespace Novin.Bpmn.Engine.Application.Queries.GetUserTask;


public sealed class GetUserTaskQueryHandler : IRequestHandler<GetUserTaskQuery, UserTaskDto?>
{
    private readonly IUserTaskInstanceRepository _userTaskRepository;
    private readonly ILogger<GetUserTaskQueryHandler> _logger;

    public GetUserTaskQueryHandler(
        IUserTaskInstanceRepository userTaskRepository,
        ILogger<GetUserTaskQueryHandler> logger)
    {
        _userTaskRepository = userTaskRepository ?? throw new ArgumentNullException(nameof(userTaskRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UserTaskDto?> Handle(GetUserTaskQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting user task: {UserTaskId}", request.UserTaskId);

        var task = await _userTaskRepository.GetByIdAsync(request.UserTaskId, cancellationToken);

        if (task is null)
        {
            _logger.LogWarning("User task not found: {UserTaskId}", request.UserTaskId);
            return null;
        }

        return new UserTaskDto
        {
            UserTaskId = task.Id,
            Status = task.Status.ToString(),
            Assignee = task.ClaimedByUserId,
            CreatedAt = task.CreatedAtUtc,
            Variables = task.VariablesObject
        };
    }
}