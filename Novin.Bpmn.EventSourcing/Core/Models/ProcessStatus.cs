using System;
using System.Collections.Generic;
using System.Text;

namespace Novin.Bpmn.EventSourcing.Core;

/// <summary>
/// وضعیت نمونه فرآیند BPMN
/// </summary>
public enum ProcessStatus
{
    /// <summary>
    /// ایجاد شده
    /// </summary>
    Created = 0,
    
    /// <summary>
    /// در حال اجرا
    /// </summary>
    Running = 1,
    
    /// <summary>
    /// تکمیل شده
    /// </summary>
    Completed = 2,
    
    /// <summary>
    /// خاتمه یافته
    /// </summary>
    Terminated = 3,
    
    /// <summary>
    /// خغلق شده
    /// </summary>
    Cancelled = 4,
    
    /// <summary>
    /// شکست خورده
    /// </summary>
    Failed = 5,
    
    /// <summary>
    /// تعلیق شده
    /// </summary>
    Suspended = 6
}
