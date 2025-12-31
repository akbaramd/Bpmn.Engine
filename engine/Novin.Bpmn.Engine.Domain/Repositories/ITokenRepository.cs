using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Common.Interfaces;

/// <summary>
/// Repository interface for Token aggregate
/// </summary>
public interface ITokenRepository : IRepository<Token>
{
    Task<IEnumerable<Token>> GetByProcessIdAsync(Guid processId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Token>> GetByStateAsync(Guid processId, TokenState state, CancellationToken cancellationToken = default);
    Task<IEnumerable<Token>> GetByElementIdAsync(Guid processId, string elementId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Token>> GetChildTokensAsync(Guid parentTokenId, CancellationToken cancellationToken = default);
    Task UpdateAsync(Token token, CancellationToken cancellationToken = default);
    Task<IEnumerable<Token>> GetByActivityInstanceIdAsync(Guid processId, Guid activityInstanceId, CancellationToken trxCt);
    Task<int> CountArrivedAtAsync(Guid processId, string elementId, Guid scopeId, bool executableOnly, CancellationToken ct);

    Task<List<Token>> GetArrivedAtAsync(Guid processId, string elementId, Guid scopeId, bool executableOnly, CancellationToken ct);
}   

