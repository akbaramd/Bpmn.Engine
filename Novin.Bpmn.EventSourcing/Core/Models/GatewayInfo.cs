using System.Collections.Generic;

namespace Novin.Bpmn.EventSourcing.Core.Models;

/// <summary>
/// اطلاعات گیت‌وی برای مدیریت بهتر Split/Join
/// </summary>
public class GatewayInfo
{
    /// <summary>
    /// شناسه گیت‌وی
    /// </summary>
    public string GatewayId { get; set; }
    
    /// <summary>
    /// نوع گیت‌وی
    /// </summary>
    public string GatewayType { get; set; }
    
    /// <summary>
    /// مسیرهای ورودی به گیت‌وی
    /// </summary>
    public ICollection<string> IncomingFlows { get; set; } = new List<string>();
    
    /// <summary>
    /// مسیرهای خروجی از گیت‌وی
    /// </summary>
    public ICollection<string> OutgoingFlows { get; set; } = new List<string>();
} 