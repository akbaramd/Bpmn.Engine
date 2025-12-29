using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

public interface ITokenForkService
{
    Task ForkChildrenAsync(
        Process process,
        Token parent,
        IReadOnlyList<BpmnSequenceFlow> outgoing,
        Guid scopeId,
        Func<BpmnSequenceFlow, bool> isExecutableForFlow,
        BpmnRuntimeContext ctx,
        CancellationToken ct);
}