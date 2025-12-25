using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Repositories;

public class EfIncidentRepository : IIncidentRepository
{
    private readonly BpmnEngineDbContext _context;
    private readonly ILogger<EfIncidentRepository> _logger;

    public EfIncidentRepository(BpmnEngineDbContext context, ILogger<EfIncidentRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Incident?> GetByIdAsync(Guid incidentId, CancellationToken ct = default)
    {
        return await _context.Incidents
            .FirstOrDefaultAsync(i => i.Id == incidentId, ct);
    }

    public async Task<IEnumerable<Incident>> GetByProcessIdAsync(Guid processId, CancellationToken ct = default)
    {
        return await _context.Incidents
            .Where(i => i.ProcessId == processId)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Incident>> GetByTokenIdAsync(Guid tokenId, CancellationToken ct = default)
    {
        return await _context.Incidents
            .Where(i => i.TokenId == tokenId)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Incident>> GetOpenIncidentsAsync(CancellationToken ct = default)
    {
        return await _context.Incidents
            .Where(i => i.Status == Domain.ValueObjects.IncidentStatus.Open)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Incident incident, CancellationToken ct = default)
    {
        await _context.Incidents.AddAsync(incident, ct);
        _logger.LogDebug("Incident added: {IncidentId}", incident.Id);
    }

    public async Task<IEnumerable<Incident>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Incidents.ToListAsync(ct);
    }

    public Task UpdateAsync(Incident incident, CancellationToken ct = default)
    {
        _context.Incidents.Update(incident);
        _logger.LogDebug("Incident updated: {IncidentId}", incident.Id);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Incident incident, CancellationToken ct = default)
    {
        _context.Incidents.Remove(incident);
        _logger.LogDebug("Incident deleted: {IncidentId}", incident.Id);
        return Task.CompletedTask;
    }
}

