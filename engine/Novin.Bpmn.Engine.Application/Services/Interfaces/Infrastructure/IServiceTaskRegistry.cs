using Microsoft.Extensions.Logging;

namespace Novin.Bpmn.Engine.Application.Services;

public interface IServiceTaskRegistry
{
    bool TryGet(string taskId, out Func<ServiceTaskExecutionContext, CancellationToken, Task> handler);
}