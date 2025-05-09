using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Novin.Bpmn.Contracts
{
    /// <summary>
    /// رابط پایه برای تمام دسترسی‌های داده در موتور BPMN
    /// این رابط پایه برای تعریف الگوی مشترک دسترسی به داده در انواع مختلف ذخیره‌سازی استفاده می‌شود
    /// </summary>
    /// <typeparam name="TEntity">نوع موجودیت اصلی</typeparam>
    /// <typeparam name="TKey">نوع کلید شناسایی</typeparam>
    public interface IBpmnDataAccessorBase<TEntity, TKey> where TEntity : class
    {
        /// <summary>
        /// دریافت یک موجودیت با کلید آن
        /// </summary>
        Task<TEntity> GetByIdAsync(TKey id);
        
        /// <summary>
        /// ذخیره یک موجودیت جدید یا به‌روزرسانی موجودیت موجود
        /// </summary>
        Task SaveAsync(TEntity entity);
        
        /// <summary>
        /// حذف یک موجودیت با کلید آن
        /// </summary>
        Task<bool> DeleteAsync(TKey id);
        
        /// <summary>
        /// دریافت تمام موجودیت‌ها
        /// </summary>
        Task<IEnumerable<TEntity>> GetAllAsync();
        
        /// <summary>
        /// جستجو در موجودیت‌ها با استفاده از شرایط فیلتر
        /// </summary>
        Task<IEnumerable<TEntity>> FindAsync(Func<TEntity, bool> predicate);
    }
} 