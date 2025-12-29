namespace Novin.Bpmn.Engine.Application.Services;

public sealed class EmptyServiceTaskRegistry : IServiceTaskRegistry
{
    public bool TryGet(string taskId, out Func<ServiceTaskExecutionContext, CancellationToken, Task> handler)
    {
        handler = default!;
        return false;
    }
}