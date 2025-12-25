using Novin.Bpmn.Engine.Domain.Common;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Domain.Entities;

/// <summary>
/// Incident: رکورد پایدار برای خطاهای تکنیکی یا BPMN errors که handle نشده‌اند
/// </summary>
public sealed class Incident : BaseEntity
{
    public Guid ProcessId { get; private set; }
    public Guid TokenId { get; private set; }
    public string ElementId { get; private set; }
    
    /// <summary>
    /// نوع خطا: BPMN Error یا Technical Failure
    /// </summary>
    public ErrorType Type { get; private set; }
    
    /// <summary>
    /// Error code (برای BPMN errors)
    /// </summary>
    public string? ErrorCode { get; private set; }
    
    /// <summary>
    /// پیام خطا
    /// </summary>
    public string Message { get; private set; }
    
    /// <summary>
    /// Stack trace (برای Technical Failures)
    /// </summary>
    public string? StackTrace { get; private set; }
    
    /// <summary>
    /// وضعیت Incident
    /// </summary>
    public IncidentStatus Status { get; private set; }
    
    /// <summary>
    /// تعداد retry های انجام شده
    /// </summary>
    public int Retries { get; private set; }
    
    /// <summary>
    /// زمان آخرین وقوع
    /// </summary>
    public DateTime LastOccurredAt { get; private set; }
    
    /// <summary>
    /// زمان ایجاد
    /// </summary>
    public DateTime CreatedAt { get; private set; }
    
    /// <summary>
    /// زمان حل شدن (اگر resolved باشد)
    /// </summary>
    public DateTime? ResolvedAt { get; private set; }

    private Incident()
    {
        // EF Core
    }

    public Incident(
        Guid processId,
        Guid tokenId,
        string elementId,
        ErrorType type,
        string message,
        string? errorCode = null,
        string? stackTrace = null)
    {
        if (string.IsNullOrWhiteSpace(elementId))
            throw new ArgumentException("ElementId cannot be null or empty", nameof(elementId));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be null or empty", nameof(message));

        ProcessId = processId;
        TokenId = tokenId;
        ElementId = elementId;
        Type = type;
        Message = message;
        ErrorCode = errorCode;
        StackTrace = stackTrace;
        Status = IncidentStatus.Open;
        Retries = 0;
        CreatedAt = DateTime.UtcNow;
        LastOccurredAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Retry کردن این Incident
    /// </summary>
    public void Retry()
    {
        if (Status != IncidentStatus.Open)
            throw new InvalidOperationException($"Cannot retry incident in {Status} status.");

        Retries++;
        LastOccurredAt = DateTime.UtcNow;
    }

    /// <summary>
    /// حل کردن Incident
    /// </summary>
    public void Resolve()
    {
        if (Status != IncidentStatus.Open)
            throw new InvalidOperationException($"Cannot resolve incident in {Status} status.");

        Status = IncidentStatus.Resolved;
        ResolvedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// باز کردن مجدد Incident (برای retry بعد از resolve)
    /// </summary>
    public void Reopen()
    {
        if (Status != IncidentStatus.Resolved)
            throw new InvalidOperationException($"Cannot reopen incident in {Status} status.");

        Status = IncidentStatus.Open;
        ResolvedAt = null;
    }
}

