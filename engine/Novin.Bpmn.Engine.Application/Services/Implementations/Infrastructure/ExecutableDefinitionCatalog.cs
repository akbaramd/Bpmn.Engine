using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.DomainServices;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Repositories;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services.Implementations.Infrastructure;

/// <summary>
/// Catalog service that manages compilation and caching of BPMN definitions.
/// Strategy: Memory → DB (compiled snapshot) → XML (compile on-the-fly)
/// </summary>
public sealed class ExecutableDefinitionCatalog : IExecutableDefinitionCatalog
{
    private readonly IBpmnDefinitionMemoryStore _memoryStore;
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IBpmnQuery _bpmnQuery;
    private readonly ILogger<ExecutableDefinitionCatalog> _logger;

    public ExecutableDefinitionCatalog(
        IBpmnDefinitionMemoryStore memoryStore,
        IDeploymentRepository deploymentRepository,
        IBpmnQuery bpmnQuery,
        ILogger<ExecutableDefinitionCatalog> logger)
    {
        _memoryStore = memoryStore ?? throw new ArgumentNullException(nameof(memoryStore));
        _deploymentRepository = deploymentRepository ?? throw new ArgumentNullException(nameof(deploymentRepository));
        _bpmnQuery = bpmnQuery ?? throw new ArgumentNullException(nameof(bpmnQuery));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ExecutableProcessDefinition> GetAsync(ProcessDefinitionRef defRef, CancellationToken ct)
    {
        // 1. Try memory first
        if (_memoryStore.TryGet(defRef, out var cached))
        {
            _logger.LogDebug("Definition found in memory: {Ref}", defRef.ToCacheKey());
            return cached;
        }

        // 2. Load from DB (Deployment)
        var deployment = await _deploymentRepository.GetByIdAsync(defRef.DeploymentId, ct);
        if (deployment is null)
            throw new InvalidOperationException($"Deployment {defRef.DeploymentId} not found");

        if (deployment.Version != defRef.Version)
        {
            _logger.LogWarning("Deployment version mismatch: expected {Expected}, got {Actual}",
                defRef.Version, deployment.Version);
        }

        // 3. Parse/compile from XML
        var definitions = deployment.GetDefinitions();
        var process = _bpmnQuery.GetProcessOrThrow(deployment, defRef.ProcessBpmnId);

        var compiled = new ExecutableProcessDefinition(
            defRef,
            process,
            definitions,
            DateTime.UtcNow);

        // 4. Store in memory
        await _memoryStore.SetAsync(compiled, ct);

        _logger.LogInformation("Definition compiled and cached: {Ref}", defRef.ToCacheKey());
        return compiled;
    }

    public async Task WarmUpAllAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting warm-up of all active definitions...");

        // Get all active deployments
        var deployments = await _deploymentRepository.GetActiveDeploymentsAsync(ct);
        var allRefs = new List<ProcessDefinitionRef>();

        foreach (var deployment in deployments)
        {
            try
            {
                var processes = _bpmnQuery.GetAllProcesses(deployment);
                foreach (var process in processes)
                {
                    var defRef = new ProcessDefinitionRef(
                        deployment.Id,
                        process.id,
                        deployment.Version);
                    allRefs.Add(defRef);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract processes from deployment {DeploymentId}", deployment.Id);
            }
        }

        _logger.LogInformation("Found {Count} process definitions to warm up", allRefs.Count);

        // Batch load with limited parallelism
        const int batchSize = 200;
        const int maxParallelism = 4;

        var semaphore = new SemaphoreSlim(maxParallelism);
        var tasks = new List<Task>();

        for (int i = 0; i < allRefs.Count; i += batchSize)
        {
            var batch = allRefs.Skip(i).Take(batchSize).ToList();
            
            foreach (var defRef in batch)
            {
                await semaphore.WaitAsync(ct);
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await GetAsync(defRef, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to warm up definition {Ref}", defRef.ToCacheKey());
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, ct));
            }
        }

        await Task.WhenAll(tasks);
        _logger.LogInformation("Warm-up complete. {Count} definitions in memory", _memoryStore.Count);
    }

    public async Task OnDefinitionChangedAsync(ProcessDefinitionRef defRef, CancellationToken ct)
    {
        _logger.LogInformation("Definition changed, invalidating and reloading: {Ref}", defRef.ToCacheKey());

        // Invalidate from memory
        await _memoryStore.InvalidateAsync(defRef, ct);

        // Reload (will compile and cache)
        await GetAsync(defRef, ct);
    }
}

