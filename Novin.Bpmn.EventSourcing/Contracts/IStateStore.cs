using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Novin.Bpmn.EventSourcing.Core;

namespace Novin.Bpmn.EventSourcing.Contracts;

/// <summary>
/// مخزن ذخیره‌سازی وضعیت
/// </summary>
public interface IStateStore
{
    /// <summary>
    /// بازیابی وضعیت
    /// </summary>
    /// <param name="processInstanceId">شناسه نمونه فرآیند</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>وضعیت بازیابی شده یا null اگر وجود نداشته باشد</returns>
    Task<BpmnProcessState?> GetStateAsync(string processInstanceId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// بازیابی وضعیت همراه با شماره نسخه
    /// </summary>
    /// <param name="processInstanceId">شناسه نمونه فرآیند</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>وضعیت بازیابی شده و شماره نسخه، یا null اگر وجود نداشته باشد</returns>
    Task<(BpmnProcessState? State, long Version)> GetStateWithVersionAsync(string processInstanceId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// ذخیره وضعیت
    /// </summary>
    /// <param name="processInstanceId">شناسه نمونه فرآیند</param>
    /// <param name="state">وضعیت برای ذخیره</param>
    /// <param name="expectedVersion">شماره نسخه مورد انتظار (null برای عدم بررسی نسخه)</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>شماره نسخه جدید</returns>
    Task<long> SaveStateAsync(string processInstanceId, BpmnProcessState state, long? expectedVersion = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// حذف وضعیت
    /// </summary>
    /// <param name="processInstanceId">شناسه نمونه فرآیند</param>
    /// <param name="expectedVersion">شماره نسخه مورد انتظار (null برای عدم بررسی نسخه)</param>
    /// <param name="cancellationToken">توکن لغو</param>
    Task DeleteStateAsync(string processInstanceId, long? expectedVersion = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// بررسی وجود یک وضعیت با شناسه نمونه فرآیند مشخص
    /// </summary>
    /// <param name="processInstanceId">شناسه نمونه فرآیند</param>
    /// <returns>آیا وضعیت موجود است</returns>
    Task<bool> HasStateAsync(string processInstanceId);
    
    /// <summary>
    /// دریافت نسخه فعلی یک وضعیت
    /// </summary>
    /// <param name="processInstanceId">شناسه نمونه فرآیند</param>
    /// <returns>نسخه فعلی وضعیت (-1 اگر وجود نداشته باشد)</returns>
    Task<long> GetVersionAsync(string processInstanceId);
    
    /// <summary>
    /// یافتن وضعیت‌های منطبق با الگوی مشخص و یک شرط
    /// </summary>
    /// <param name="pattern">الگوی جستجو در شناسه‌های نمونه فرآیند (می‌تواند * داشته باشد)</param>
    /// <param name="predicate">شرط فیلتر روی وضعیت (اختیاری)</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>لیست وضعیت‌های یافت شده</returns>
    Task<List<BpmnProcessState>> FindStatesByPatternAsync(
        string pattern,
        Func<BpmnProcessState, bool>? predicate = null,
        CancellationToken cancellationToken = default);
} 