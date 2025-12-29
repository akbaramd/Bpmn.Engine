using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.Common.Interfaces;

/// <summary>
/// Repository برای Incident entities
/// </summary>
public interface IIncidentRepository
{
    Task<Incident?> GetByIdAsync(Guid incidentId, CancellationToken ct = default);
    Task<IEnumerable<Incident>> GetByProcessIdAsync(Guid processId, CancellationToken ct = default);
    Task<IEnumerable<Incident>> GetByTokenIdAsync(Guid tokenId, CancellationToken ct = default);
    Task<IEnumerable<Incident>> GetOpenIncidentsAsync(CancellationToken ct = default);
    Task<IEnumerable<Incident>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Incident incident, CancellationToken ct = default);
    Task UpdateAsync(Incident incident, CancellationToken ct = default);
    Task DeleteAsync(Incident incident, CancellationToken ct = default);
}

