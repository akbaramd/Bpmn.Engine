namespace Novin.Bpmn.EventSourcing.Core.Models;

/// <summary>
/// وضعیت وظیفه
/// </summary>
public enum TaskStatus
{
    /// <summary>
    /// ایجاد شده
    /// </summary>
    Created,
    
    /// <summary>
    /// فعال
    /// </summary>
    Active,
    
    /// <summary>
    /// در حال پردازش
    /// </summary>
    Processing,
    
    /// <summary>
    /// تکمیل شده
    /// </summary>
    Completed,
    
    /// <summary>
    /// لغو شده
    /// </summary>
    Cancelled,
    
    /// <summary>
    /// خطا
    /// </summary>
    Error
}
