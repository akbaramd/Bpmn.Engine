// Domain/DomainServices/Repositories.cs
using System;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Domain.DomainServices;

/// <summary>
/// Domain repository ports. Implement in Infrastructure (EF Core).
/// Keep them small: instantiation needs only GetById.
/// </summary>
public interface IProjectRepository
{
    Project? GetById(Guid projectId);
}

