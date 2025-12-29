using Novin.Bpmn.Engine.Domain.Repositories;

namespace Novin.Bpmn.Engine.Application.Common.Interfaces;

/// <summary>
/// Unit of Work pattern interface for managing transactions and domain events.
/// All data persistence happens only during commit operations.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    IDeploymentRepository Deployments { get; }
    IProcessRepository Processes { get; }
    IUserTaskInstanceRepository UserTaskInstances { get; }
    ITokenRepository Tokens { get; }
    IWorkerRepository Workers { get; }
    IIncidentRepository Incidents { get; }
    IBoundarySubscriptionRepository BoundarySubscriptions { get; }
    INodeInstanceRepository NodeInstances { get; }


    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a transaction is currently active
    /// </summary>
    bool IsInTransaction { get; }
    
    /// <summary>
    /// Track an aggregate for change tracking and event dispatching on commit
    /// </summary>
}

