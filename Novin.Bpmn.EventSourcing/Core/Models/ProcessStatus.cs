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
    /// شکست خورده
    /// </summary>
    Failed = 4,
    
    /// <summary>
    /// تعلیق شده
    /// </summary>
    Suspended = 5,
    
    /// <summary>
    /// حذف شده
    /// </summary>
    Deleted = 6
}
