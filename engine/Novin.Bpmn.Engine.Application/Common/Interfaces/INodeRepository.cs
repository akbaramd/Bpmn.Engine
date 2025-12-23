using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Common.Interfaces;

/// <summary>
/// Repository interface for Node aggregate
/// </summary>
public interface INodeRepository : IRepository<Node>
{
    Task<IEnumerable<Node>> GetByProcessIdAsync(Guid processId, CancellationToken cancellationToken = default);
    Task<Node?> GetByElementIdAsync(Guid processId, string elementId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Node>> GetByStateAsync(Guid processId, NodeState state, CancellationToken cancellationToken = default);
    Task<IEnumerable<Node>> GetByTypeAsync(Guid processId, NodeType nodeType, CancellationToken cancellationToken = default);
}

