using System;

namespace Novin.Bpmn.EventSourcing.Contracts;

/// <summary>
/// مدل رد شدن دستور در موتور فرآیند BPMN
/// </summary>
public record BpmnRejection
{
    /// <summary>
    /// شناسه منحصر به فرد این رد شدن
    /// </summary>
    public Guid RejectionId { get; init; } = Guid.NewGuid();
    
    /// <summary>
    /// شناسه دستور رد شده
    /// </summary>
    public required Guid CommandId { get; init; }
    
    /// <summary>
    /// دلیل رد شدن
    /// </summary>
    public required string Reason { get; init; }
    
    /// <summary>
    /// کد خطا
    /// </summary>
    public required string Code { get; init; }
    
    /// <summary>
    /// اطلاعات بیشتر (اختیاری)
    /// </summary>
    public string? Details { get; init; }
    
    /// <summary>
    /// زمان رد شدن
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// نسخه (برای سازگاری)
    /// </summary>
    public int Version { get; init; } = 1;
} 