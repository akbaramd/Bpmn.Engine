using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Common;
using Novin.Bpmn.Engine.Infrastructure.EventBus;
using Novin.Bpmn.Engine.Infrastructure.Persistence;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.UnitOfWork;

/// <summary>
/// Entity Framework Core implementation of Unit of Work pattern
/// Uses DbContext for transaction management and persistence
/// </summary>
public class EfUnitOfWork : IUnitOfWork
{
    private readonly BpmnEngineDbContext _context;
    private readonly DomainEventDispatcher _domainEventDispatcher;
    private readonly ILogger<EfUnitOfWork> _logger;
    private bool _disposed = false;

    public IDeploymentRepository Deployments { get; }
    public IProcessRepository Processes { get; }
    public INodeRepository Nodes { get; }
    public ITokenRepository Tokens { get; }
    public ITaskRepository Tasks { get; }

    public EfUnitOfWork(
        BpmnEngineDbContext context,
        IDeploymentRepository deploymentRepository,
        IProcessRepository processRepository,
        INodeRepository nodeRepository,
        ITokenRepository tokenRepository,
        ITaskRepository taskRepository,
        DomainEventDispatcher domainEventDispatcher,
        ILogger<EfUnitOfWork> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        Deployments = deploymentRepository ?? throw new ArgumentNullException(nameof(deploymentRepository));
        Processes = processRepository ?? throw new ArgumentNullException(nameof(processRepository));
        Nodes = nodeRepository ?? throw new ArgumentNullException(nameof(nodeRepository));
        Tokens = tokenRepository ?? throw new ArgumentNullException(nameof(tokenRepository));
        Tasks = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _domainEventDispatcher = domainEventDispatcher ?? throw new ArgumentNullException(nameof(domainEventDispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
    }

  public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    // 1. Get all domain events from tracked entities
    var domainEntities = _context.ChangeTracker
        .Entries<IAggregateRoot>() // Assuming your entities implement this
        .Where(x => x.Entity.DomainEvents.Any())
        .Select(x => x.Entity)
        .ToList();

    var domainEvents = domainEntities
        .SelectMany(x => x.DomainEvents)
        .ToList();

    // 2. Clear events from entities to prevent double-dispatch
    domainEntities.ForEach(entity => entity.ClearDomainEvents());

    // 3. Save changes
    var result = await _context.SaveChangesAsync(cancellationToken);

    // 4. Dispatch events (which might trigger more DB changes)
    await _domainEventDispatcher.DispatchEventsAsync(domainEvents, cancellationToken);

    return result;
}

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
           

            // 3. Save changes
            var result = await SaveChangesAsync(cancellationToken);

       
            
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
    }


    /// <summary>
    /// Track an aggregate for change tracking and event dispatching
    /// </summary>
    public void TrackAggregate(IAggregateRoot aggregate)
    {
        if (aggregate == null)
            throw new ArgumentNullException(nameof(aggregate));

    }

    public void Dispose()
    {
        if (!_disposed)
        {

            _disposed = true;
        }
    }
}

