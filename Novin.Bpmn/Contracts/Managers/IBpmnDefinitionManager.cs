using Novin.Bpmn.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Novin.Bpmn.Contracts
{
    /// <summary>
    /// رابط مدیریت تعاریف فرآیندهای BPMN
    /// </summary>
    public interface IBpmnDefinitionManager
    {
        /// <summary>
        /// دسترسی به ذخیره‌ساز تعاریف فرآیند
        /// </summary>
        IBpmnDefinitionAccessor DefinitionAccessor { get; }
        
        /// <summary>
        /// بارگذاری یک فرآیند جدید با کلید و محتوا
        /// </summary>
        Task<BpmnDefinitionInfo> DeployProcessAsync(string deploymentKey, string definitionXml, string label = null);
        
        /// <summary>
        /// بارگذاری یک فرآیند جدید با کلید و استریم محتوا
        /// </summary>
        Task<BpmnDefinitionInfo> DeployProcessAsync(string deploymentKey, Stream definitionStream, string label = null);
        
        /// <summary>
        /// دریافت یک تعریف فرآیند با کلید
        /// </summary>
        Task<BpmnDefinitionInfo> GetProcessDefinitionAsync(string deploymentKey);
        
        /// <summary>
        /// دریافت همه تعاریف فرآیندها
        /// </summary>
        Task<IEnumerable<BpmnDefinitionInfo>> GetAllProcessDefinitionsAsync();
        
        /// <summary>
        /// حذف یک تعریف فرآیند
        /// </summary>
        Task<bool> DeleteProcessDefinitionAsync(string deploymentKey);
        
        /// <summary>
        /// اعتبارسنجی یک تعریف BPMN
        /// </summary>
        Task<bool> ValidateDefinitionAsync(string definitionXml);
    }
} 