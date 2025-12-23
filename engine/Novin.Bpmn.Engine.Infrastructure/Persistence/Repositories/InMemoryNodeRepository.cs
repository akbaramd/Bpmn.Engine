using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Task = System.Threading.Tasks.Task;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Repositories;

public class InMemoryNodeRepository : INodeRepository
{
    private readonly ConcurrentDictionary<Guid, Node> _nodes = new();
    private readonly ILogger<InMemoryNodeRepository> _logger;

    public InMemoryNodeRepository(ILogger<InMemoryNodeRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<Node?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _nodes.TryGetValue(id, out var node);
        return Task.FromResult(node);
    }

    public Task<IEnumerable<Node>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_nodes.Values.AsEnumerable());
    }

    public Task AddAsync(Node aggregate, CancellationToken cancellationToken = default)
    {
        if (!_nodes.TryAdd(aggregate.Id, aggregate))
        {
            throw new InvalidOperationException($"Node with ID {aggregate.Id} already exists.");
        }
        
        _logger.LogInformation("Node added: {NodeId}", aggregate.Id);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Node aggregate, CancellationToken cancellationToken = default)
    {
        _nodes.AddOrUpdate(aggregate.Id, aggregate, (key, oldValue) => aggregate);
        _logger.LogInformation("Node updated: {NodeId}", aggregate.Id);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _nodes.TryRemove(id, out _);
        _logger.LogInformation("Node deleted: {NodeId}", id);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<Node>> GetByProcessIdAsync(Guid processId, CancellationToken cancellationToken = default)
    {
        var nodes = _nodes.Values.Where(n => n.ProcessId == processId);
        return Task.FromResult(nodes);
    }

    public Task<Node?> GetByElementIdAsync(Guid processId, string elementId, CancellationToken cancellationToken = default)
    {
        var node = _nodes.Values.FirstOrDefault(n => n.ProcessId == processId && n.ElementId == elementId);
        return Task.FromResult(node);
    }

    public Task<IEnumerable<Node>> GetByStateAsync(Guid processId, NodeState state, CancellationToken cancellationToken = default)
    {
        var nodes = _nodes.Values.Where(n => n.ProcessId == processId && n.State == state);
        return Task.FromResult(nodes);
    }

    public Task<IEnumerable<Node>> GetByTypeAsync(Guid processId, NodeType nodeType, CancellationToken cancellationToken = default)
    {
        var nodes = _nodes.Values.Where(n => n.ProcessId == processId && n.Type == nodeType);
        return Task.FromResult(nodes);
    }
}

