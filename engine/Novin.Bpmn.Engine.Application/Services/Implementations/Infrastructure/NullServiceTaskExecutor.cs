using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

public sealed class NullServiceTaskExecutor : IServiceTaskExecutor
{
    private readonly ILogger<NullServiceTaskExecutor> _logger;

    public NullServiceTaskExecutor(ILogger<NullServiceTaskExecutor> logger)
        => _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public Task ExecuteAsync(Process process, Token token, BpmnServiceTask task, CancellationToken ct)
    {
        var taskId = task?.id ?? "<null>";
        _logger.LogError("IServiceTaskExecutor is not configured. ServiceTaskId={TaskId}", taskId);

        token.Fail($"ServiceTask executor not configured for '{taskId}'.");
        return Task.CompletedTask;
    }
}