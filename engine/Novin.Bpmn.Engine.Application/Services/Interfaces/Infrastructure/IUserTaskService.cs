using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

public interface IUserTaskService
{
    Task<Guid> CreateAsync(Process process, Token token, BpmnUserTask ut, CancellationToken ct);
}