using Novin.Bpmn.Contracts;
using Novin.Bpmn.V3;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Novin.Bpmn.Core
{
    /// <summary>
    /// رابط دسترسی به نمونه‌های فرآیند BPMN
    /// </summary>
    public interface IBpmnProcessInstanceAccessor : IBpmnDataAccessorBase<BpmnV3ProcessInstance, string>
    {
        /// <summary>
        /// دریافت یک نمونه فرآیند با شناسه
        /// </summary>
        Task<BpmnV3ProcessInstance> GetInstanceAsync(string instanceId);
        
        /// <summary>
        /// دریافت یک نمونه فرآیند با شناسه توکن
        /// </summary>
        Task<BpmnV3ProcessInstance> GetInstanceByTokenAsync(Guid tokenId);
        
        /// <summary>
        /// دریافت همه نمونه‌های فرآیندهای در حال اجرا
        /// </summary>
        Task<IEnumerable<BpmnV3ProcessInstance>> GetAllActiveInstancesAsync();
        
        /// <summary>
        /// دریافت همه نمونه‌های فرآیندهای یک تعریف خاص
        /// </summary>
        Task<IEnumerable<BpmnV3ProcessInstance>> GetInstancesByDeploymentKeyAsync(string deploymentKey);
        
        /// <summary>
        /// دریافت همه نمونه‌های فرآیندهای یک فرآیند خاص
        /// </summary>
        Task<IEnumerable<BpmnV3ProcessInstance>> GetInstancesByProcessIdAsync(string processId);
        
        /// <summary>
        /// ذخیره یک نمونه فرآیند
        /// </summary>
        Task SaveInstanceAsync(BpmnV3ProcessInstance instance);
        
        /// <summary>
        /// به‌روزرسانی وضعیت یک نمونه فرآیند
        /// </summary>
        Task<bool> UpdateInstanceStatusAsync(string instanceId, ProcessInstanceStatus status);
        
        /// <summary>
        /// حذف یک نمونه فرآیند
        /// </summary>
        Task<bool> DeleteInstanceAsync(string instanceId);
    }
    
    /// <summary>
    /// وضعیت‌های ممکن برای یک نمونه فرآیند
    /// </summary>
    public enum ProcessInstanceStatus
    {
        Active,        // در حال اجرا
        Completed,     // تکمیل شده
        Suspended,     // معلق شده
        Terminated,    // خاتمه یافته
        Error          // با خطا مواجه شده
    }
} 