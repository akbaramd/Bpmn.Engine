using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing.Contracts;

/// <summary>
/// مخزن ذخیره‌سازی وضعیت
/// </summary>
public interface IStateStore
{
    /// <summary>
    /// بازیابی وضعیت
    /// </summary>
    /// <typeparam name="T">نوع وضعیت</typeparam>
    /// <param name="key">کلید وضعیت</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>وضعیت بازیابی شده یا null اگر وجود نداشته باشد</returns>
    Task<T?> GetStateAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
    
    /// <summary>
    /// بازیابی وضعیت همراه با شماره نسخه
    /// </summary>
    /// <typeparam name="T">نوع وضعیت</typeparam>
    /// <param name="key">کلید وضعیت</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>وضعیت بازیابی شده و شماره نسخه، یا null اگر وجود نداشته باشد</returns>
    Task<(T? State, long Version)> GetStateWithVersionAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
    
    /// <summary>
    /// ذخیره وضعیت
    /// </summary>
    /// <typeparam name="T">نوع وضعیت</typeparam>
    /// <param name="key">کلید وضعیت</param>
    /// <param name="state">وضعیت برای ذخیره</param>
    /// <param name="expectedVersion">شماره نسخه مورد انتظار (null برای عدم بررسی نسخه)</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>شماره نسخه جدید</returns>
    Task<long> SaveStateAsync<T>(string key, T state, long? expectedVersion = null, CancellationToken cancellationToken = default) where T : class;
    
    /// <summary>
    /// حذف وضعیت
    /// </summary>
    /// <param name="key">کلید وضعیت</param>
    /// <param name="expectedVersion">شماره نسخه مورد انتظار (null برای عدم بررسی نسخه)</param>
    /// <param name="cancellationToken">توکن لغو</param>
    Task DeleteStateAsync(string key, long? expectedVersion = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// بررسی وجود یک وضعیت با کلید مشخص
    /// </summary>
    /// <param name="key">کلید منحصر به فرد</param>
    /// <returns>آیا وضعیت موجود است</returns>
    Task<bool> HasStateAsync(string key);
    
    /// <summary>
    /// دریافت نسخه فعلی یک وضعیت
    /// </summary>
    /// <param name="key">کلید منحصر به فرد</param>
    /// <returns>نسخه فعلی وضعیت (-1 اگر وجود نداشته باشد)</returns>
    Task<long> GetVersionAsync(string key);
    
    /// <summary>
    /// یافتن وضعیت‌های منطبق با الگوی مشخص و یک شرط
    /// </summary>
    /// <typeparam name="T">نوع وضعیت</typeparam>
    /// <param name="pattern">الگوی جستجو در کلیدها (می‌تواند * داشته باشد)</param>
    /// <param name="predicate">شرط فیلتر روی وضعیت (اختیاری)</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>لیست وضعیت‌های یافت شده</returns>
    Task<List<T>> FindStatesByPatternAsync<T>(
        string pattern,
        Func<T, bool>? predicate = null,
        CancellationToken cancellationToken = default) where T : class;
} 