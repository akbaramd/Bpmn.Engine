using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Novin.Bpmn.EventSourcing.Core.Deployment;
using Novin.Bpmn.Models;

namespace Novin.Bpmn.EventSourcing.Contracts
{
    /// <summary>
    /// واسط ذخیره‌سازی تعاریف BPMN در حافظه
    /// این واسط همیشه داده‌ها را در حافظه نگه می‌دارد و با مخزن اصلی همگام می‌شود
    /// </summary>
    public interface IBpmnDefinitionStorage
    {
        /// <summary>
        /// مقداردهی اولیه و بارگذاری تعاریف از مخزن
        /// </summary>
        Task InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// افزودن تعریف BPMN جدید به حافظه
        /// </summary>
        /// <param name="deploymentKey">کلید یکتای نصب</param>
        /// <param name="definitionInfo">اطلاعات نصب</param>
        /// <param name="parsedDefinition">تعریف پارس شده</param>
        /// <returns>شناسه تعریف</returns>
        string AddDefinition(
            string deploymentKey,
            BpmnDeploymentInfo definitionInfo,
            BpmnDefinitions parsedDefinition);

        /// <summary>
        /// دریافت اطلاعات تعریف با کلید مشخص
        /// </summary>
        /// <param name="deploymentKey">کلید نصب</param>
        /// <returns>اطلاعات نصب یا null اگر یافت نشد</returns>
        BpmnDeploymentInfo GetDeploymentInfo(string deploymentKey);

        /// <summary>
        /// دریافت تعریف پارس شده با کلید مشخص
        /// </summary>
        /// <param name="deploymentKey">کلید نصب</param>
        /// <returns>تعریف پارس شده یا null اگر یافت نشد</returns>
        BpmnDefinitions GetParsedDefinition(string deploymentKey);

        /// <summary>
        /// دریافت تمام کلیدهای نصب موجود
        /// </summary>
        /// <returns>لیست کلیدهای نصب</returns>
        IReadOnlyList<string> GetAllDeploymentKeys();

        /// <summary>
        /// جستجوی تعاریف بر اساس شرط
        /// </summary>
        /// <param name="predicate">شرط جستجو</param>
        /// <returns>نتایج جستجو</returns>
        IReadOnlyList<BpmnDeploymentInfo> FindDeployments(Func<BpmnDeploymentInfo, bool> predicate);

        /// <summary>
        /// جستجوی تعاریف بر اساس شناسه فرآیند
        /// </summary>
        /// <param name="processId">شناسه فرآیند</param>
        /// <returns>لیست تعاریف حاوی فرآیند موردنظر</returns>
        IReadOnlyList<BpmnDeploymentInfo> FindDeploymentsByProcessId(string processId);
        
        /// <summary>
        /// جستجوی تعاریف بر اساس کلید پیام
        /// </summary>
        /// <param name="messageKey">کلید پیام</param>
        /// <returns>لیست تعاریف حاوی پیام موردنظر</returns>
        IReadOnlyList<BpmnDeploymentInfo> FindDeploymentsByMessageKey(string messageKey);
        
        /// <summary>
        /// جستجوی تعاریف بر اساس نام رویداد
        /// </summary>
        /// <param name="eventName">نام رویداد</param>
        /// <returns>لیست تعاریف حاوی رویداد موردنظر</returns>
        IReadOnlyList<BpmnDeploymentInfo> FindDeploymentsByEventName(string eventName);
        
        /// <summary>
        /// جستجوی تعاریف بر اساس نوع المان
        /// </summary>
        /// <param name="elementType">نوع المان</param>
        /// <returns>لیست تعاریف حاوی المان موردنظر</returns>
        IReadOnlyList<BpmnDeploymentInfo> FindDeploymentsByElementType(string elementType);
        
        /// <summary>
        /// دریافت متادیتای فرآیند
        /// </summary>
        /// <param name="deploymentKey">کلید نصب</param>
        /// <returns>متادیتای فرآیند یا null اگر یافت نشد</returns>
        ProcessMetadata GetProcessMetadata(string deploymentKey);

        /// <summary>
        /// آیا تعریف با کلید مشخص وجود دارد
        /// </summary>
        /// <param name="deploymentKey">کلید نصب</param>
        /// <returns>وضعیت وجود تعریف</returns>
        bool HasDefinition(string deploymentKey);

        /// <summary>
        /// حذف تعریف از حافظه
        /// </summary>
        /// <param name="deploymentKey">کلید نصب</param>
        /// <returns>آیا حذف موفقیت‌آمیز بود</returns>
        bool RemoveDefinition(string deploymentKey);
        
        /// <summary>
        /// دریافت تعداد تعاریف موجود در حافظه
        /// </summary>
        int Count { get; }
    }
} 