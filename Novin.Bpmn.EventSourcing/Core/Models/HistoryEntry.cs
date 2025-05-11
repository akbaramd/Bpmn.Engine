namespace Novin.Bpmn.EventSourcing.Core;

/// <summary>
/// ورودی تاریخچه رویدادها
/// </summary>
public class HistoryEntry
{
    /// <summary>
    /// شناسه رویداد
    /// </summary>
    public Guid EventId { get; set; }
    
    /// <summary>
    /// نوع رویداد
    /// </summary>
    public string? EventType { get; set; }
    
    /// <summary>
    /// قصد رویداد
    /// </summary>
    public string? Intent { get; set; }
    
    /// <summary>
    /// زمان رویداد
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// شناسه کاربر
    /// </summary>
    public string? UserId { get; set; }
} 