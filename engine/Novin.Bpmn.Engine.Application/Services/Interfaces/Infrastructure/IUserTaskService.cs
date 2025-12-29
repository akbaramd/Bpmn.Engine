using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

public interface IUserTaskService
{
    Task<Guid> CreateOrGetAsync(
        Process process,
        Token token,
        NodeInstance node,
        BpmnUserTask userTask,
        CancellationToken ct);
}