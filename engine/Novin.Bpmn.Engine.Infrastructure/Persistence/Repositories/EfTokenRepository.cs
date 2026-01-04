using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Task = System.Threading.Tasks.Task;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Repositories;

public class EfTokenRepository : ITokenRepository
{
    private readonly BpmnEngineDbContext _context;
    private readonly ILogger<EfTokenRepository> _logger;

    public EfTokenRepository(BpmnEngineDbContext context, ILogger<EfTokenRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Token?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Tokens
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Token>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Tokens
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Token aggregate, CancellationToken cancellationToken = default)
    {
        await _context.Tokens.AddAsync(aggregate, cancellationToken);
        _logger.LogInformation("Token added: {TokenId}", aggregate.Id);
    }



    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var token = await GetByIdAsync(id, cancellationToken);
        if (token != null)
        {
            _context.Tokens.Remove(token);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Token deleted: {TokenId}", id);
        }
    }

    public async Task UpdateAsync(Token token, CancellationToken cancellationToken = default)
    {
        _context.Tokens.Update(token);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Token updated: {TokenId}", token.Id);
    }

    public async Task<IEnumerable<Token>> GetByProcessIdAsync(Guid processId, CancellationToken cancellationToken = default)
    {
        return await _context.Tokens
            .Where(t => t.ProcessId == processId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Token>> GetByStateAsync(Guid processId, TokenState state, CancellationToken cancellationToken = default)
    {
        return await _context.Tokens
            .Where(t => t.ProcessId == processId && t.State == state)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Token>> GetByElementIdAsync(Guid processId, string elementId, CancellationToken cancellationToken = default)
    {
        return await _context.Tokens
            .Where(t => t.ProcessId == processId && t.CurrentElementId == elementId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Token>> GetChildTokensAsync(Guid parentTokenId, CancellationToken cancellationToken = default)
    {
        return await _context.Tokens
            .Where(t => t.ParentTokenId == parentTokenId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Token>> GetByActivityInstanceIdAsync(Guid processId, Guid activityInstanceId,
        CancellationToken trxCt)
    {
      
        return await _context.Tokens
            .Where(t => t.ProcessId == processId && t.ActivityInstanceId == activityInstanceId )
            .ToListAsync(trxCt);
    }

    public async Task<int> CountArrivedAtAsync(
        Guid processId,
        string elementId,
        Guid scopeId,
        bool executableOnly,
        CancellationToken ct)
    {
        // ✅ Count Active, Waiting, Terminated, and Merged tokens (arrived at join gateway)
        // Merged tokens are child tokens that have merged at the join gateway
        // Completed tokens have moved to next element, so they shouldn't be counted
        var q = _context.Tokens.AsNoTracking()
            .Where(t =>
                t.ProcessId == processId &&
                t.CurrentElementId == elementId &&
                t.ScopeId == scopeId &&
                (t.State == TokenState.Active || t.State == TokenState.Waiting || t.State == TokenState.Terminated || t.State == TokenState.Merged));

        // executableOnly parameter is deprecated - all tokens are executable

        return await q.CountAsync(ct);
    }

    public async Task<List<Token>> GetArrivedAtAsync(
        Guid processId,
        string elementId,
        Guid scopeId,
        bool executableOnly,
        CancellationToken ct)
    {
        if (processId == Guid.Empty) throw new ArgumentException("processId is empty.", nameof(processId));
        if (string.IsNullOrWhiteSpace(elementId)) throw new ArgumentException("elementId is null/empty.", nameof(elementId));
        if (scopeId == Guid.Empty) throw new ArgumentException("scopeId is empty.", nameof(scopeId));

        // ✅ Include Active, Waiting, Terminated, and Merged tokens (arrived at join gateway)
        // Merged tokens are child tokens that have merged at the join gateway
        var q = _context.Tokens.Where(t =>
            t.ProcessId == processId &&
            t.CurrentElementId == elementId &&
            t.ScopeId == scopeId &&
            (t.State == TokenState.Active || t.State == TokenState.Waiting || t.State == TokenState.Terminated || t.State == TokenState.Merged));

        // executableOnly parameter is deprecated - all tokens are executable

        // Tracking لازم است چون بعداً همین توکن‌ها را Merge/Reactivate می‌کنی.
        return await q
            .OrderBy(t => t.Id) // deterministic
            .ToListAsync(ct);
    }
}

