using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Common;
using Novin.Bpmn.Engine.Infrastructure.EventBus;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.UnitOfWork;

public class EfUnitOfWork : IUnitOfWork
{
    private readonly BpmnEngineDbContext _context;
    private readonly DomainEventDispatcher _domainEventDispatcher;
    private readonly ILogger<EfUnitOfWork> _logger;

    private IDbContextTransaction? _currentTransaction;
    private bool _disposed;

    public IDeploymentRepository Deployments { get; }
    public IProcessRepository Processes { get; }
    public ITokenRepository Tokens { get; }
    public ITaskRepository Tasks { get; }
    public IIncidentRepository Incidents { get; }
    public IBoundarySubscriptionRepository BoundarySubscriptions { get; }

    /// <summary>
    /// Checks if a transaction is currently active
    /// </summary>
    public bool IsInTransaction => _currentTransaction != null;

    public EfUnitOfWork(
        BpmnEngineDbContext context,
        IDeploymentRepository deploymentRepository,
        IProcessRepository processRepository,
        ITokenRepository tokenRepository,
        ITaskRepository taskRepository,
        IIncidentRepository incidentRepository,
        IBoundarySubscriptionRepository boundarySubscriptionRepository,
        DomainEventDispatcher domainEventDispatcher,
        ILogger<EfUnitOfWork> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        Deployments = deploymentRepository ?? throw new ArgumentNullException(nameof(deploymentRepository));
        Processes = processRepository ?? throw new ArgumentNullException(nameof(processRepository));
        Tokens = tokenRepository ?? throw new ArgumentNullException(nameof(tokenRepository));
        Tasks = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        Incidents = incidentRepository ?? throw new ArgumentNullException(nameof(incidentRepository));
        BoundarySubscriptions = boundarySubscriptionRepository ?? throw new ArgumentNullException(nameof(boundarySubscriptionRepository));
        _domainEventDispatcher = domainEventDispatcher ?? throw new ArgumentNullException(nameof(domainEventDispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ---------------- Transaction API ----------------

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));

        // Nested / already in tx: فقط action
        if (_currentTransaction != null)
        {
            await action(ct);
            return;
        }

        await BeginTransactionAsync(ct);

        try
        {
            await action(ct);
            await CommitTransactionAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExecuteInTransactionAsync failed. Rolling back.");
            try
            {
                await RollbackTransactionAsync(ct);
            }
            catch (Exception rbEx)
            {
                _logger.LogError(rbEx, "Rollback failed.");
            }

            throw;
        }
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null) return;

        _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        // اگر transaction explicit نداریم، فقط save کن
        if (_currentTransaction == null)
        {
            await SaveChangesAsync(cancellationToken);
            return;
        }

        try
        {
            await SaveChangesAsync(cancellationToken);
            await _currentTransaction.CommitAsync(cancellationToken);
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null) return;

        try
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;

            // برای جلوگیری از state ناسازگار بعد از rollback
            _context.ChangeTracker.Clear();
        }
    }

    // ---------------- SaveChanges + Domain Events ----------------

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // این الگوریتم:
        // - رویدادها را جمع می‌کند
        // - clear می‌کند
        // - SaveChanges می‌کند
        // - Dispatch می‌کند
        // تا وقتی که نه رویداد داریم نه تغییرات pending
        var total = 0;

        while (true)
        {
            var domainEntities = _context.ChangeTracker
                .Entries<IAggregateRoot>()
                .Where(x => x.Entity.DomainEvents.Any())
                .Select(x => x.Entity)
                .ToList();

            var domainEvents = domainEntities
                .SelectMany(x => x.DomainEvents)
                .ToList();

            domainEntities.ForEach(e => e.ClearDomainEvents());

            var hasChanges = _context.ChangeTracker.HasChanges();
            if (!hasChanges && domainEvents.Count == 0)
                break;

            // 1) Persist
            total += await _context.SaveChangesAsync(cancellationToken);

            // 2) Dispatch (ممکن است تغییرات/رویدادهای جدید بسازد)
            if (domainEvents.Count > 0)
                await _domainEventDispatcher.DispatchEventsAsync(domainEvents, cancellationToken);
        }

        return total;
    }

    // ---------------- Tracking ----------------

    public void TrackAggregate(IAggregateRoot aggregate)
    {
        if (aggregate == null) throw new ArgumentNullException(nameof(aggregate));

        var entry = _context.Entry(aggregate);
        if (entry.State == EntityState.Detached)
            _context.Attach(aggregate);
    }

    // ---------------- Dispose ----------------

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _currentTransaction?.Dispose();
        _context.Dispose();
    }
}
