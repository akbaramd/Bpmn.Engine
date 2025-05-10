using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Novin.Bpmn.EventSourcing.Core.Deployment;
using Novin.Bpmn.Models;

namespace Novin.Bpmn.EventSourcing.Contracts
{
    /// <summary>
    /// واسط مخزن تعاریف BPMN با قابلیت ذخیره‌سازی دائمی و کش در حافظه
    /// </summary>
    public interface IBpmnDefinitionStore
    {
        /// <summary>
        /// مقداردهی اولیه و بارگذاری تعاریف از حافظه دائمی
        /// </summary>
        Task InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// ذخیره تعریف BPMN جدید
        /// </summary>
        /// <param name="deploymentKey">کلید یکتای نصب</param>
        /// <param name="xmlContent">محتوای XML</param>
        /// <param name="parsedDefinitions">تعریف پارس شده</param>
        /// <param name="label">برچسب (اختیاری)</param>
        /// <param name="cancellationToken">توکن لغو عملیات</param>
        /// <returns>شناسه تعریف</returns>
        Task<string> SaveDefinitionAsync(
            string deploymentKey,
            string xmlContent,
            BpmnDefinitions parsedDefinitions,
            string label = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// بازیابی اطلاعات تعریف BPMN بر اساس کلید نصب
        /// </summary>
        /// <param name="deploymentKey">کلید نصب</param>
        /// <param name="cancellationToken">توکن لغو عملیات</param>
        /// <returns>اطلاعات نصب یا null اگر یافت نشد</returns>
        Task<BpmnDeploymentInfo> GetDeploymentInfoAsync(
            string deploymentKey,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// بازیابی تعریف پارس‌شده BPMN بر اساس کلید نصب
        /// </summary>
        /// <param name="deploymentKey">کلید نصب</param>
        /// <param name="xmlParser">تابع پارس کردن XML</param>
        /// <param name="cancellationToken">توکن لغو عملیات</param>
        /// <returns>تعریف پارس شده یا null اگر یافت نشد</returns>
        Task<BpmnDefinitions> GetParsedDefinitionAsync(
            string deploymentKey,
            Func<string, BpmnDefinitions> xmlParser,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// دریافت تمام کلیدهای نصب موجود
        /// </summary>
        /// <param name="cancellationToken">توکن لغو عملیات</param>
        /// <returns>لیست کلیدهای نصب</returns>
        Task<IList<string>> GetAllDeploymentKeysAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// حذف تعریف BPMN
        /// </summary>
        /// <param name="deploymentKey">کلید نصب</param>
        /// <param name="cancellationToken">توکن لغو عملیات</param>
        Task DeleteDefinitionAsync(string deploymentKey, CancellationToken cancellationToken = default);
    }
} 