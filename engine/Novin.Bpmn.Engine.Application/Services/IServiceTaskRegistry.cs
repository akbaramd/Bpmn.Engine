using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;


public sealed record ServiceTaskExecutionContext(Process Process, Token Token, BpmnServiceTask Task);

public interface IServiceTaskRegistry
{
    bool TryGet(string taskId, out Func<ServiceTaskExecutionContext, CancellationToken, Task> handler);
}

public sealed class EmptyServiceTaskRegistry : IServiceTaskRegistry
{
    public bool TryGet(string taskId, out Func<ServiceTaskExecutionContext, CancellationToken, Task> handler)
    {
        handler = default!;
        return false;
    }
}
public interface IServiceTaskExecutor
{
    /// <summary>
    /// Execute the business/integration logic of a ServiceTask for the given process/token.
    /// Implementations should call token.Fail(...) on business failure (or throw and let caller map it).
    /// </summary>
    Task ExecuteAsync(Process process, Token token, BpmnServiceTask task, CancellationToken ct);
}

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