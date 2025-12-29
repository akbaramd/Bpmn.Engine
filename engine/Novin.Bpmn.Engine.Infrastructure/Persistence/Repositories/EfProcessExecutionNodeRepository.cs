using Microsoft.EntityFrameworkCore;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Repositories;

/// <summary>
/// Entity Framework implementation of ExecutedNode repository
/// </summary>
public class EfProcessExecutionNodeRepository : IProcessExecutionNodeRepository
{
    private readonly BpmnEngineDbContext _context;

    public EfProcessExecutionNodeRepository(BpmnEngineDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    // Basic CRUD operations
    public async Task<ExecutedNode?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ProcessExecutionNodes.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<IEnumerable<ExecutedNode>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ProcessExecutionNodes.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(ExecutedNode entity, CancellationToken cancellationToken = default)
    {
        await _context.ProcessExecutionNodes.AddAsync(entity, cancellationToken);
    }

    public async Task UpdateAsync(ExecutedNode entity, CancellationToken cancellationToken = default)
    {
        _context.ProcessExecutionNodes.Update(entity);
        await Task.CompletedTask; // Make method properly async
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ProcessExecutionNodes.FindAsync(new object[] { id }, cancellationToken);
        if (entity != null)
        {
            _context.ProcessExecutionNodes.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    // Domain-specific methods
    public async Task<IEnumerable<ExecutedNode>> GetByProcessIdAsync(
        Guid processId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ProcessExecutionNodes
            .Where(n => n.ProcessId == processId)
            .OrderBy(n => n.SequenceOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<ExecutedNode>> GetExecutionPathAsync(
        Guid processId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ProcessExecutionNodes
            .Where(n => n.ProcessId == processId)
            .OrderBy(n => n.SequenceOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<ExecutedNode?> GetLastExecutedNodeAsync(
        Guid processId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ProcessExecutionNodes
            .Where(n => n.ProcessId == processId)
            .OrderByDescending(n => n.SequenceOrder)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> NodeExistsAsync(
        Guid processId,
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ProcessExecutionNodes
            .AnyAsync(n => n.ProcessId == processId && n.NodeId == nodeId, cancellationToken);
    }

    public async Task<ExecutedNode?> GetNodeAsync(
        Guid processId,
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ProcessExecutionNodes
            .FirstOrDefaultAsync(n => n.ProcessId == processId && n.NodeId == nodeId, cancellationToken);
    }

    public async Task<ProcessExecutionStats> GetExecutionStatsAsync(
        Guid processId,
        CancellationToken cancellationToken = default)
    {
        var nodes = await _context.ProcessExecutionNodes
            .Where(n => n.ProcessId == processId)
            .ToListAsync(cancellationToken);

        if (!nodes.Any())
        {
            return new ProcessExecutionStats();
        }

        var nodeTypeCounts = nodes
            .GroupBy(n => n.NodeType)
            .ToDictionary(g => g.Key, g => g.Count());

        var firstExecuted = nodes.Min(n => n.ExecutedAt);
        var lastExecuted = nodes.Max(n => n.ExecutedAt);
        var totalExecutionTime = lastExecuted - firstExecuted;

        return new ProcessExecutionStats
        {
            TotalNodesExecuted = nodes.Count,
            CompletedNodes = nodes.Count(n => n.IsCompleted),
            FirstExecutedAt = firstExecuted,
            LastExecutedAt = lastExecuted,
            TotalExecutionTime = totalExecutionTime,
            NodeTypeCounts = nodeTypeCounts
        };
    }
}