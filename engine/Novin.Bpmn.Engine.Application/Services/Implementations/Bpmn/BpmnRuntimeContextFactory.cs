using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.Services;

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
        var deployment = await _uow.Deployments.GetByIdAsync(process.DeploymentId, ct);
        if (deployment == null)
            throw new InvalidOperationException($"Deployment not found for {process.DeploymentId}");

        var logger = _loggerFactory?.CreateLogger<BpmnDefinitionsService>();
        var defs = new BpmnDefinitionsService(deployment.GetDefinitions(), logger);
        var pid = process.ProcessBpmnId; // Use the specific process BPMN ID from the process

        return new BpmnRuntimeContext(pid, new BpmnModelAccessor(defs));
    }
}