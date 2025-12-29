using Microsoft.EntityFrameworkCore;
using Novin.Bpmn.Engine.Domain.DomainServices;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Repositories;

public sealed class ProjectRepository : IProjectRepository
{
    private readonly BpmnEngineDbContext _db;

    public ProjectRepository(BpmnEngineDbContext db) => _db = db;

    public Project? GetById(Guid projectId)
        => _db.Set<Project>().AsNoTracking().SingleOrDefault(x => x.Id == projectId);
}