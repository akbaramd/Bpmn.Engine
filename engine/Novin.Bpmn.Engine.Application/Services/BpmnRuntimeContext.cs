using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;

public sealed record BpmnRuntimeContext(
    string BpmnProcessId,
    IBpmnModelAccessor Model
);

public interface IBpmnRuntimeContextFactory
{
    Task<BpmnRuntimeContext> CreateAsync(Process process, CancellationToken ct);
}

public sealed class BpmnRuntimeContextFactory : IBpmnRuntimeContextFactory
{
    private readonly IUnitOfWork _uow;
    private readonly ILoggerFactory? _loggerFactory;

    public BpmnRuntimeContextFactory(IUnitOfWork uow, ILoggerFactory? loggerFactory = null)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _loggerFactory = loggerFactory;
    }

    public async Task<BpmnRuntimeContext> CreateAsync(Process process, CancellationToken ct)
    {
        var deployment = await _uow.Deployments.GetLatestByDeploymentKeyAsync(process.ProcessDefinitionId, ct);
        if (deployment == null)
            throw new InvalidOperationException($"Deployment not found for {process.ProcessDefinitionId}");

        var logger = _loggerFactory?.CreateLogger<BpmnDefinitionsService>();
        var defs = new BpmnDefinitionsService(deployment.GetDefinitions(), logger);
        var pid = defs.GetFirstProcess().id ?? process.ProcessDefinitionId;

        return new BpmnRuntimeContext(pid, new BpmnModelAccessor(defs));
    }
}