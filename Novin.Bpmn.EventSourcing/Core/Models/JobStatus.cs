namespace Novin.Bpmn.EventSourcing.Core;

/// <summary>
/// وضعیت کار
/// </summary>
public enum JobStatus
{
    /// <summary>
    /// ایجاد شده
    /// </summary>
    Created,
    
    /// <summary>
    /// فعال شده
    /// </summary>
    Activated,
    
    /// <summary>
    /// تکمیل شده
    /// </summary>
    Completed,
    
    /// <summary>
    /// شکست خورده
    /// </summary>
    Failed,
    
    /// <summary>
    /// اتمام مهلت
    /// </summary>
    Timeout,
    
    /// <summary>
    /// خطا
    /// </summary>
    Error
}
