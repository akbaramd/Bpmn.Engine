using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Services;

/// <summary>
/// Service برای مدیریت Incident lifecycle
/// </summary>
public interface IIncidentService
{
    /// <summary>
    /// ایجاد یک Incident جدید برای Technical Failure
    /// Incident به UnitOfWork اضافه می‌شود اما SaveChanges صدا زده نمی‌شود.
    /// مسئولیت commit با orchestrator است که این متد را فراخوانی می‌کند.
    /// </summary>
    Task<Incident> CreateTechnicalFailureAsync(
        Guid processId,
        Guid tokenId,
        string elementId,
        string message,
        string? stackTrace = null,
        CancellationToken ct = default);

    /// <summary>
    /// ایجاد یک Incident جدید برای BPMN Error
    /// Incident به UnitOfWork اضافه می‌شود اما SaveChanges صدا زده نمی‌شود.
    /// مسئولیت commit با orchestrator است که این متد را فراخوانی می‌کند.
    /// </summary>
    Task<Incident> CreateBpmnErrorAsync(
        Guid processId,
        Guid tokenId,
        string elementId,
        string errorCode,
        string message,
        CancellationToken ct = default);

    /// <summary>
    /// Retry کردن یک Incident (فقط Retries++ می‌کند)
    /// ⚠️ توجه: این متد فقط وضعیت Incident را تغییر می‌دهد و هیچ تأثیری روی Token یا جریان ندارد.
    /// برای پیش‌بردن جریان، از ITokenManagementService.RetryTokenAsync استفاده کنید.
    /// </summary>
    Task RetryIncidentAsync(Guid incidentId, CancellationToken ct = default);

    /// <summary>
    /// Resolve کردن یک Incident (فقط Status = Resolved می‌کند)
    /// ⚠️ توجه: این متد فقط وضعیت Incident را تغییر می‌دهد و هیچ تأثیری روی Token یا جریان ندارد.
    /// برای پیش‌بردن جریان، از ITokenManagementService استفاده کنید:
    /// - RetryTokenAsync: برای retry کردن Token
    /// - MoveTokenAsync: برای انتقال Token به نود دیگر
    /// - TerminateTokenAsync: برای terminate کردن Token
    /// </summary>
    Task ResolveIncidentAsync(Guid incidentId, CancellationToken ct = default);
}

