namespace Novin.Bpmn.EventSourcing.Core;

/// <summary>
/// اطلاعات کار
/// </summary>
public class JobInfo
{
    /// <summary>
    /// شناسه کار
    /// </summary>
    public string? JobId { get; set; }
    
    /// <summary>
    /// شناسه المان مرتبط
    /// </summary>
    public string? ElementId { get; set; }
    
    /// <summary>
    /// نوع المان مرتبط
    /// </summary>
    public string? ElementType { get; set; }
    
    /// <summary>
    /// نوع کار
    /// </summary>
    public string? JobType { get; set; }
    
    /// <summary>
    /// تعداد تلاش‌های باقی‌مانده
    /// </summary>
    public int Retries { get; set; }
    
    /// <summary>
    /// ضرب‌الاجل
    /// </summary>
    public DateTime? Deadline { get; set; }
    
    /// <summary>
    /// شناسه کارگر فعال‌کننده
    /// </summary>
    public string? WorkerId { get; set; }
    
    /// <summary>
    /// هدرهای اختصاصی
    /// </summary>
    public Dictionary<string, string>? CustomHeaders { get; set; }
    
    /// <summary>
    /// نتیجه اجرا
    /// </summary>
    public Dictionary<string, object>? Result { get; set; }
    
    /// <summary>
    /// پیام خطا
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// کد خطا
    /// </summary>
    public string? ErrorCode { get; set; }
    
    /// <summary>
    /// زمان اجرای مجدد
    /// </summary>
    public DateTime? RetryBackOff { get; set; }
    
    /// <summary>
    /// وضعیت کار
    /// </summary>
    public JobStatus Status { get; set; }
    
    /// <summary>
    /// زمان ایجاد
    /// </summary>
    public DateTime? CreatedAt { get; set; }
    
    /// <summary>
    /// زمان فعال شدن
    /// </summary>
    public DateTime? ActivatedAt { get; set; }
    
    /// <summary>
    /// زمان تکمیل
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// زمان شکست
    /// </summary>
    public DateTime? FailedAt { get; set; }
    
    /// <summary>
    /// زمان اتمام مهلت
    /// </summary>
    public DateTime? TimedOutAt { get; set; }
    
    /// <summary>
    /// زمان خطا
    /// </summary>
    public DateTime? ErrorAt { get; set; }
}
