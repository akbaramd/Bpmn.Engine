using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

public interface IGatewayJoinService
{
    /// <summary>
    /// اگر این گیت‌وی از نوع Join/Barrier باشد، توکن را وارد Waiting می‌کند و
    /// اگر همه incoming های لازم رسیده باشند، Merge انجام می‌دهد و survivor را Move می‌دهد.
    /// خروجی true یعنی "join logic handled" و نباید ادامه پردازش در handler انجام شود.
    /// </summary>
    Task<bool> TryJoinAsync(
        Process process,
        Token arrivingToken,
        BpmnGateway gateway,
        BpmnRuntimeContext ctx,
        CancellationToken ct);
}