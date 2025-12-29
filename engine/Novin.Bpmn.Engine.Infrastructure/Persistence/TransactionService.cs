using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Services;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence;

/// <summary>
/// Implementation of transaction service using DbContext directly.
/// Handles transactions and ensures proper change tracking.
/// Domain events are automatically converted to outbox messages by the interceptor.
/// </summary>
public sealed class TransactionService : ITransactionService
{
    private readonly BpmnEngineDbContext _context;
    private readonly ILogger<TransactionService> _logger;

    public TransactionService(
        BpmnEngineDbContext context,
        ILogger<TransactionService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));

        IDbContextTransaction? transaction = null;
        try
        {
            transaction = await _context.Database.BeginTransactionAsync(ct);
            await action(ct);
            
            // Save changes (domain events are automatically converted to outbox messages by interceptor)
            await _context.SaveChangesAsync(ct);
            
            await transaction.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TRANSACTION] Transaction failed. Rolling back.");
            if (transaction != null)
            {
                try
                {
                    await transaction.RollbackAsync(ct);
                }
                catch (Exception rbEx)
                {
                    _logger.LogError(rbEx, "[TRANSACTION] Rollback failed.");
                }
            }
            throw;
        }
        finally
        {
            if (transaction != null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

}
