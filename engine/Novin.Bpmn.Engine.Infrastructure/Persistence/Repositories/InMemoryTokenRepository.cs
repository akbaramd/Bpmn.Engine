using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Task = System.Threading.Tasks.Task;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Repositories;

public class InMemoryTokenRepository : ITokenRepository
{
    private readonly ConcurrentDictionary<Guid, Token> _tokens = new();
    private readonly ILogger<InMemoryTokenRepository> _logger;

    public InMemoryTokenRepository(ILogger<InMemoryTokenRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<Token?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _tokens.TryGetValue(id, out var token);
        return Task.FromResult(token);
    }

    public Task<IEnumerable<Token>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_tokens.Values.AsEnumerable());
    }

    public Task AddAsync(Token aggregate, CancellationToken cancellationToken = default)
    {
        if (!_tokens.TryAdd(aggregate.Id, aggregate))
        {
            throw new InvalidOperationException($"Token with ID {aggregate.Id} already exists.");
        }
        
        _logger.LogInformation("Token added: {TokenId}", aggregate.Id);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Token aggregate, CancellationToken cancellationToken = default)
    {
        _tokens.AddOrUpdate(aggregate.Id, aggregate, (key, oldValue) => aggregate);
        _logger.LogInformation("Token updated: {TokenId}", aggregate.Id);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _tokens.TryRemove(id, out _);
        _logger.LogInformation("Token deleted: {TokenId}", id);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<Token>> GetByProcessIdAsync(Guid processId, CancellationToken cancellationToken = default)
    {
        var tokens = _tokens.Values.Where(t => t.ProcessId == processId);
        return Task.FromResult(tokens);
    }

    public Task<IEnumerable<Token>> GetByStateAsync(Guid processId, TokenState state, CancellationToken cancellationToken = default)
    {
        var tokens = _tokens.Values.Where(t => t.ProcessId == processId && t.State == state);
        return Task.FromResult(tokens);
    }

    public Task<IEnumerable<Token>> GetByElementIdAsync(Guid processId, string elementId, CancellationToken cancellationToken = default)
    {
        var tokens = _tokens.Values.Where(t => t.ProcessId == processId && t.CurrentElementId == elementId);
        return Task.FromResult(tokens);
    }

    public Task<IEnumerable<Token>> GetChildTokensAsync(Guid parentTokenId, CancellationToken cancellationToken = default)
    {
        var tokens = _tokens.Values.Where(t => t.ParentTokenId == parentTokenId);
        return Task.FromResult(tokens);
    }
}

