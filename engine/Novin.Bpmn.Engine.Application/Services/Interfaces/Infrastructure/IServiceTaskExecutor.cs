using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

public interface IServiceTaskExecutor
{
    /// <summary>
    /// Execute the business/integration logic of a ServiceTask for the given process/token.
    /// Implementations should call token.Fail(...) on business failure (or throw and let caller map it).
    /// </summary>
    Task ExecuteAsync(Process process, Token token, BpmnServiceTask task, CancellationToken ct);
}