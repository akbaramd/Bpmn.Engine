using System;
using System.Collections.Generic;

namespace Novin.Bpmn.EventSourcing.Core.Models;

/// <summary>
/// اطلاعات گیت‌وی مبتنی بر رویداد
/// </summary>
public class EventBasedGatewayInfo
{
    /// <summary>
    /// شناسه گیت‌وی
    /// </summary>
    public string GatewayId { get; set; }
    
    /// <summary>
    /// شناسه رویدادهای هدف
    /// </summary>
    public List<string> EventTargets { get; set; } = new List<string>();
    
    /// <summary>
    /// آیا گیت‌وی فعال است؟
    /// </summary>
    public bool IsActive { get; set; }
    
    /// <summary>
    /// شناسه رویداد انتخاب شده (پس از فعال شدن یکی از رویدادها)
    /// </summary>
    public string SelectedEventId { get; set; }
    
    /// <summary>
    /// زمان فعال‌سازی گیت‌وی
    /// </summary>
    public DateTime ActivatedAt { get; set; }
    
    /// <summary>
    /// زمان فعال‌سازی رویداد انتخاب شده
    /// </summary>
    public DateTime? EventTriggeredAt { get; set; }
} 