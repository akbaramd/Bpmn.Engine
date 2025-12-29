using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

public interface IGatewaySplitService
{
    /// <summary>
    /// اگر این گیت‌وی از نوع Split باشد، fork را انجام می‌دهد.
    /// خروجی true یعنی split انجام شد و نباید fallback navigation اجرا شود.
    /// </summary>
    Task<bool> TrySplitAsync(
        Process process,
        Token token,
        BpmnGateway gateway,
        BpmnRuntimeContext ctx,
        CancellationToken ct);
}