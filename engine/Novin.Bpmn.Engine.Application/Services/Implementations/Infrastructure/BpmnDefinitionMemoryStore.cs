using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Services.Implementations.Infrastructure;

/// <summary>
/// Thread-safe in-memory store for compiled BPMN definitions.
/// </summary>
public sealed class BpmnDefinitionMemoryStore : IBpmnDefinitionMemoryStore
{
    private readonly ConcurrentDictionary<string, ExecutableProcessDefinition> _store = new();
    private readonly ILogger<BpmnDefinitionMemoryStore> _logger;

    public int Count => _store.Count;

    public BpmnDefinitionMemoryStore(ILogger<BpmnDefinitionMemoryStore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool TryGet(ProcessDefinitionRef defRef, out ExecutableProcessDefinition def)
    {
        var key = defRef.ToCacheKey();
        return _store.TryGetValue(key, out def!);
    }

    public Task WarmUpAsync(IEnumerable<ProcessDefinitionRef> allRefs, CancellationToken ct)
    {
        var count = 0;
        foreach (var @ref in allRefs)
        {
            // Definitions will be loaded by catalog, not here
            // This is just for tracking
            count++;
        }

        _logger.LogInformation("Warm-up prepared for {Count} definitions", count);
        return Task.CompletedTask;
    }

    public Task SetAsync(ExecutableProcessDefinition def, CancellationToken ct)
    {
        var key = def.Ref.ToCacheKey();
        _store[key] = def;
        _logger.LogDebug("Definition cached: {Key}", key);
        return Task.CompletedTask;
    }

    public Task InvalidateAsync(ProcessDefinitionRef defRef, CancellationToken ct)
    {
        var key = defRef.ToCacheKey();
        if (_store.TryRemove(key, out _))
        {
            _logger.LogDebug("Definition invalidated: {Key}", key);
        }
        return Task.CompletedTask;
    }
}

