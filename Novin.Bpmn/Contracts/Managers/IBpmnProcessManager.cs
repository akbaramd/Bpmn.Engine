using Novin.Bpmn.Contracts.Accessors;
using Novin.Bpmn.Core;
using Novin.Bpmn.Core.Managers;
using Novin.Bpmn.V3;

namespace Novin.Bpmn.Contracts.Managers
{
    /// <summary>
    /// رابط مدیریت نمونه‌های فرآیندهای BPMN
    /// </summary>
    public interface IBpmnProcessManager
    {
        /// <summary>
        /// دسترسی به ذخیره‌ساز نمونه‌های فرآیند
        /// </summary>
        IBpmnProcessInstanceAccessor ProcessInstanceAccessor { get; }
        
        /// <summary>
        /// ایجاد یک نمونه فرآیند جدید
        /// </summary>
        Task<BpmnV3ProcessInstance> CreateProcessInstanceAsync(string deploymentKey, string processId, string definitionXml, Dictionary<string, object> variables = null);
        
        /// <summary>
        /// شروع اجرای یک نمونه فرآیند
        /// </summary>
        Task<BpmnV3ProcessInstance> StartProcessAsync(BpmnV3ProcessInstance instance);
        
        /// <summary>
        /// ادامه اجرای یک فرآیند از آخرین وضعیت ذخیره شده
        /// </summary>
        Task<BpmnV3ProcessInstance> ResumeProcessAsync(string instanceId);
        
        /// <summary>
        /// به‌روزرسانی وضعیت یک نمونه فرآیند
        /// </summary>
        Task<bool> UpdateProcessStatusAsync(string instanceId, ProcessInstanceStatus status);
        
        /// <summary>
        /// دریافت یک نمونه فرآیند با شناسه
        /// </summary>
        Task<BpmnV3ProcessInstance> GetProcessInstanceAsync(string instanceId);
        
        /// <summary>
        /// دریافت یک نمونه فرآیند با شناسه توکن
        /// </summary>
        Task<BpmnV3ProcessInstance> GetProcessInstanceByTokenAsync(Guid tokenId);
        
        /// <summary>
        /// دریافت همه نمونه‌های فرآیندهای در حال اجرا
        /// </summary>
        Task<IEnumerable<BpmnV3ProcessInstance>> GetAllActiveInstancesAsync();
        
        /// <summary>
        /// دریافت همه نمونه‌های فرآیندهای یک تعریف خاص
        /// </summary>
        Task<IEnumerable<BpmnV3ProcessInstance>> GetInstancesByDeploymentKeyAsync(string deploymentKey);
        
        /// <summary>
        /// ذخیره یک نمونه فرآیند
        /// </summary>
        Task<bool> SaveProcessInstanceAsync(BpmnV3ProcessInstance instance);
        
        /// <summary>
        /// خاتمه دادن به یک نمونه فرآیند
        /// </summary>
        Task<bool> TerminateProcessInstanceAsync(string instanceId);
        
        /// <summary>
        /// حذف یک نمونه فرآیند
        /// </summary>
        Task<bool> DeleteProcessInstanceAsync(string instanceId);
        
        /// <summary>
        /// تبدیل نمونه فرآیند به مدل جزئیات
        /// </summary>
        ProcessInstanceDetails CreateProcessInstanceDetails(BpmnV3ProcessInstance instance);
        
        /// <summary>
        /// دریافت پردازنده برای یک نمونه فرآیند
        /// </summary>
        Task<BpmnProcessManager> GetExecutorForInstanceAsync(string instanceId);
        
        /// <summary>
        /// دریافت پردازنده برای یک توکن
        /// </summary>
        Task<BpmnProcessManager> GetExecutorForTokenAsync(Guid tokenId);
    }
} 