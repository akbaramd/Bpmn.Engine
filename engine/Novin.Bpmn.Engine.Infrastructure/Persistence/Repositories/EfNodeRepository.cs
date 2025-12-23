using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Task = System.Threading.Tasks.Task;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Repositories;

public class EfNodeRepository : INodeRepository
{
    private readonly BpmnEngineDbContext _context;
    private readonly ILogger<EfNodeRepository> _logger;

    public EfNodeRepository(BpmnEngineDbContext context, ILogger<EfNodeRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Node?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Nodes
            .Include(n => n.TokenHistory)
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Node>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Nodes
            .Include(n => n.TokenHistory)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Node aggregate, CancellationToken cancellationToken = default)
    {
        await _context.Nodes.AddAsync(aggregate, cancellationToken);
        _logger.LogInformation("Node added: {NodeId}", aggregate.Id);
    }

    public Task UpdateAsync(Node aggregate, CancellationToken cancellationToken = default)
    {
        _context.Nodes.Update(aggregate);
        _logger.LogInformation("Node updated: {NodeId}", aggregate.Id);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var node = await GetByIdAsync(id, cancellationToken);
        if (node != null)
        {
            _context.Nodes.Remove(node);
            _logger.LogInformation("Node deleted: {NodeId}", id);
        }
    }

    public async Task<IEnumerable<Node>> GetByProcessIdAsync(Guid processId, CancellationToken cancellationToken = default)
    {
        return await _context.Nodes
            .Include(n => n.TokenHistory)
            .Where(n => n.ProcessId == processId)
            .ToListAsync(cancellationToken);
    }

    public async Task<Node?> GetByElementIdAsync(Guid processId, string elementId, CancellationToken cancellationToken = default)
    {
        return await _context.Nodes
            .Include(n => n.TokenHistory)
            .FirstOrDefaultAsync(n => n.ProcessId == processId && n.ElementId == elementId, cancellationToken);
    }

    public async Task<IEnumerable<Node>> GetByStateAsync(Guid processId, NodeState state, CancellationToken cancellationToken = default)
    {
        return await _context.Nodes
            .Include(n => n.TokenHistory)
            .Where(n => n.ProcessId == processId && n.State == state)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Node>> GetByTypeAsync(Guid processId, NodeType nodeType, CancellationToken cancellationToken = default)
    {
        return await _context.Nodes
            .Include(n => n.TokenHistory)
            .Where(n => n.ProcessId == processId && n.Type == nodeType)
            .ToListAsync(cancellationToken);
    }
}

