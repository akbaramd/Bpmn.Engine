using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Repositories;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Services;

/// <summary>
/// Factory for creating BpmnRuntimeContext using memory definitions (catalog).
/// ✅ Uses IExecutableDefinitionCatalog instead of direct Deployment repository access.
/// </summary>
public sealed class BpmnRuntimeContextFactory : IBpmnRuntimeContextFactory
{
    private readonly IUnitOfWork _uow;
    private readonly IExecutableDefinitionCatalog _catalog;
    private readonly ILoggerFactory? _loggerFactory;

    public BpmnRuntimeContextFactory(
        IUnitOfWork uow,
        IExecutableDefinitionCatalog catalog,
        ILoggerFactory? loggerFactory = null)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _loggerFactory = loggerFactory;
    }

    public async Task<BpmnRuntimeContext> CreateAsync(Process process, CancellationToken ct)
    {
        // Get deployment to build ProcessDefinitionRef
        var deployment = await _uow.Deployments.GetByIdAsync(process.DeploymentId, ct);
        if (deployment == null)
            throw new InvalidOperationException($"Deployment not found for {process.DeploymentId}");

        // ✅ Use catalog (memory-first)
        var defRef = ProcessDefinitionRef.From(process, deployment);
        var compiled = await _catalog.GetAsync(defRef, ct);

        // Create BpmnDefinitionsService from compiled definition
        var logger = _loggerFactory?.CreateLogger<BpmnDefinitionsService>();
        var defs = new BpmnDefinitionsService(compiled.Definitions, logger);
        var pid = process.ProcessBpmnId; // Use the specific process BPMN ID from the process

        return new BpmnRuntimeContext(pid, new BpmnModelAccessor(defs));
    }
}