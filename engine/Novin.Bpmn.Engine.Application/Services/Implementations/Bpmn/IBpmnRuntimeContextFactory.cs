using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.Services;

public interface IBpmnRuntimeContextFactory
{
    Task<BpmnRuntimeContext> CreateAsync(Process process, CancellationToken ct);
}