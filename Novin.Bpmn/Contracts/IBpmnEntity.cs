using System;

namespace Novin.Bpmn.Contracts
{
    /// <summary>
    /// رابط پایه برای موجودیت‌های BPMN
    /// این رابط مشخص می‌کند که یک موجودیت دارای کلید شناسایی است
    /// </summary>
    /// <typeparam name="TKey">نوع کلید شناسایی</typeparam>
    public interface IBpmnEntity<TKey>
    {
        /// <summary>
        /// کلید شناسایی موجودیت
        /// </summary>
        TKey Id { get; }
    }
}