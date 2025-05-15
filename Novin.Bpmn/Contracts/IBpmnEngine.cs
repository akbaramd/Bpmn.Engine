using Novin.Bpmn.Core;
using Novin.Bpmn.V3;
using Novin.Bpmn.V3.UserTasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Novin.Bpmn.Contracts.Accessors;
using Novin.Bpmn.Contracts.Managers;

namespace Novin.Bpmn.Contracts
{
    /// <summary>
    /// رابط اصلی موتور BPMN
    /// </summary>
    public interface IBpmnEngine
    {
        /// <summary>
        /// دسترسی به مدیریت فرآیندها
        /// </summary>
        IBpmnProcessManager ProcessManager { get; }
        
        /// <summary>
        /// دسترسی به مدیریت تعاریف فرآیندها
        /// </summary>
        IBpmnDefinitionManager DefinitionManager { get; }
        
        /// <summary>
        /// دسترسی به مدیریت وظایف کاربری
        /// </summary>
        IBpmnTaskManager TaskManager { get; }
        
        #region Process Definition Operations
        
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
        
        #endregion
        
        #region Process Instance Operations
        
        /// <summary>
        /// شروع یک نمونه فرآیند جدید
        /// </summary>
        Task<BpmnV3ProcessInstance> StartProcessAsync(string deploymentKey, string processId, Dictionary<string, object> variables = null);
        
        /// <summary>
        /// دریافت یک نمونه فرآیند با شناسه
        /// </summary>
        Task<BpmnV3ProcessInstance> GetProcessInstanceAsync(string instanceId);
        
        /// <summary>
        /// دریافت همه نمونه‌های فرآیندهای در حال اجرا
        /// </summary>
        Task<IEnumerable<BpmnV3ProcessInstance>> GetAllActiveProcessInstancesAsync();
        
        /// <summary>
        /// دریافت همه نمونه‌های فرآیندهای یک تعریف خاص
        /// </summary>
        Task<IEnumerable<BpmnV3ProcessInstance>> GetProcessInstancesByDeploymentKeyAsync(string deploymentKey);
        
        /// <summary>
        /// خاتمه دادن به یک نمونه فرآیند
        /// </summary>
        Task<bool> TerminateProcessInstanceAsync(string instanceId);
        
        /// <summary>
        /// حذف یک نمونه فرآیند
        /// </summary>
        Task<bool> DeleteProcessInstanceAsync(string instanceId);
        
        /// <summary>
        /// ادامه اجرای یک فرآیند از آخرین وضعیت ذخیره شده
        /// </summary>
        Task<BpmnV3ProcessInstance> ResumeProcessAsync(string instanceId);
        
        #endregion
        
        #region Task Operations
        
        /// <summary>
        /// دریافت وظایف کاربری یک کاربر
        /// </summary>
        Task<List<BpmnV3UserTaskAssignment>> GetUserTasksAsync(string userId, List<string> userGroups = null);
        
        /// <summary>
        /// دریافت وظایف کاربری یک فرآیند
        /// </summary>
        Task<List<BpmnV3UserTaskAssignment>> GetProcessTasksAsync(string instanceId);
        
        /// <summary>
        /// تخصیص یک وظیفه به کاربر
        /// </summary>
        Task<BpmnV3UserTaskAssignment> ClaimTaskAsync(Guid tokenId, string userId);
        
        /// <summary>
        /// تکمیل یک وظیفه کاربری
        /// </summary>
        Task<BpmnV3ProcessInstance> CompleteTaskAsync(Guid tokenId, string userId, Dictionary<string, object> formData = null);
        
        #endregion
    }
} 