using Novin.Bpmn.Contracts;
using Novin.Bpmn.Core;
using Novin.Bpmn.V3;
using Novin.Bpmn.V3.UserTasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Novin.Bpmn
{
    /// <summary>
    /// پیاده‌سازی موتور اصلی BPMN
    /// </summary>
    public class BpmnEngine : IBpmnEngine
    {
        private readonly IBpmnProcessManager _processManager;
        private readonly IBpmnDefinitionManager _definitionManager;
        private readonly IBpmnTaskManager _taskManager;

        public IBpmnProcessManager ProcessManager => _processManager;
        
        public IBpmnDefinitionManager DefinitionManager => _definitionManager;
        
        public IBpmnTaskManager TaskManager => _taskManager;

        public BpmnEngine(
            IBpmnProcessManager processManager,
            IBpmnDefinitionManager definitionManager,
            IBpmnTaskManager taskManager)
        {
            _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
            _definitionManager = definitionManager ?? throw new ArgumentNullException(nameof(definitionManager));
            _taskManager = taskManager ?? throw new ArgumentNullException(nameof(taskManager));
        }

        #region Process Definition Operations
        
        /// <summary>
        /// بارگذاری یک فرآیند جدید با کلید و محتوا
        /// </summary>
        public async Task<BpmnDefinitionInfo> DeployProcessAsync(string deploymentKey, string definitionXml, string label = null)
        {
            return await _definitionManager.DeployProcessAsync(deploymentKey, definitionXml, label);
        }
        
        /// <summary>
        /// بارگذاری یک فرآیند جدید با کلید و استریم محتوا
        /// </summary>
        public async Task<BpmnDefinitionInfo> DeployProcessAsync(string deploymentKey, Stream definitionStream, string label = null)
        {
            return await _definitionManager.DeployProcessAsync(deploymentKey, definitionStream, label);
        }
        
        /// <summary>
        /// دریافت یک تعریف فرآیند با کلید
        /// </summary>
        public async Task<BpmnDefinitionInfo> GetProcessDefinitionAsync(string deploymentKey)
        {
            return await _definitionManager.GetProcessDefinitionAsync(deploymentKey);
        }
        
        /// <summary>
        /// دریافت همه تعاریف فرآیندها
        /// </summary>
        public async Task<IEnumerable<BpmnDefinitionInfo>> GetAllProcessDefinitionsAsync()
        {
            return await _definitionManager.GetAllProcessDefinitionsAsync();
        }
        
        /// <summary>
        /// حذف یک تعریف فرآیند
        /// </summary>
        public async Task<bool> DeleteProcessDefinitionAsync(string deploymentKey)
        {
            return await _definitionManager.DeleteProcessDefinitionAsync(deploymentKey);
        }
        
        #endregion
        
        #region Process Instance Operations
        
        /// <summary>
        /// شروع یک نمونه فرآیند جدید
        /// </summary>
        public async Task<BpmnV3ProcessInstance> StartProcessAsync(string deploymentKey, string processId, Dictionary<string, object> variables = null)
        {
            // دریافت تعریف فرآیند
            var definition = await _definitionManager.GetProcessDefinitionAsync(deploymentKey);
            if (definition == null)
            {
                throw new ArgumentException($"Process definition with key '{deploymentKey}' not found");
            }

            // ایجاد نمونه فرآیند
            var instance = await _processManager.CreateProcessInstanceAsync(
                deploymentKey, 
                processId, 
                definition.DefinitionXml, 
                variables);
            
            // شروع اجرای فرآیند
            var startedInstance = await _processManager.StartProcessAsync(instance);
            
            // برگرداندن نمونه فرآیند
            return startedInstance;
        }
        
        /// <summary>
        /// دریافت یک نمونه فرآیند با شناسه
        /// </summary>
        public async Task<BpmnV3ProcessInstance> GetProcessInstanceAsync(string instanceId)
        {
            return await _processManager.GetProcessInstanceAsync(instanceId);
        }
        
        /// <summary>
        /// دریافت همه نمونه‌های فرآیندهای در حال اجرا
        /// </summary>
        public async Task<IEnumerable<BpmnV3ProcessInstance>> GetAllActiveProcessInstancesAsync()
        {
            return await _processManager.GetAllActiveInstancesAsync();
        }
        
        /// <summary>
        /// دریافت همه نمونه‌های فرآیندهای یک تعریف خاص
        /// </summary>
        public async Task<IEnumerable<BpmnV3ProcessInstance>> GetProcessInstancesByDeploymentKeyAsync(string deploymentKey)
        {
            return await _processManager.GetInstancesByDeploymentKeyAsync(deploymentKey);
        }
        
        /// <summary>
        /// خاتمه دادن به یک نمونه فرآیند
        /// </summary>
        public async Task<bool> TerminateProcessInstanceAsync(string instanceId)
        {
            return await _processManager.TerminateProcessInstanceAsync(instanceId);
        }
        
        /// <summary>
        /// حذف یک نمونه فرآیند
        /// </summary>
        public async Task<bool> DeleteProcessInstanceAsync(string instanceId)
        {
            return await _processManager.DeleteProcessInstanceAsync(instanceId);
        }
        
        /// <summary>
        /// ادامه اجرای یک فرآیند از آخرین وضعیت ذخیره شده
        /// </summary>
        public async Task<BpmnV3ProcessInstance> ResumeProcessAsync(string instanceId)
        {
            return await _processManager.ResumeProcessAsync(instanceId);
        }
        
        #endregion
        
        #region Task Operations
        
        /// <summary>
        /// دریافت وظایف کاربری یک کاربر
        /// </summary>
        public async Task<List<BpmnV3UserTaskAssignment>> GetUserTasksAsync(string userId, List<string> userGroups = null)
        {
            return await _taskManager.GetAvailableTasksForUserAsync(userId, userGroups);
        }
        
        /// <summary>
        /// دریافت وظایف کاربری یک فرآیند
        /// </summary>
        public async Task<List<BpmnV3UserTaskAssignment>> GetProcessTasksAsync(string instanceId)
        {
            return await _taskManager.GetTaskAssignmentsForProcessInstanceAsync(instanceId);
        }
        
        /// <summary>
        /// تخصیص یک وظیفه به کاربر
        /// </summary>
        public async Task<BpmnV3UserTaskAssignment> ClaimTaskAsync(Guid tokenId, string userId)
        {
            await _taskManager.ClaimTaskAssignmentAsync(tokenId, userId);
            return await _taskManager.GetTaskAssignmentAsync(tokenId);
        }
        
        /// <summary>
        /// تکمیل یک وظیفه کاربری
        /// </summary>
        public async Task<BpmnV3ProcessInstance> CompleteTaskAsync(Guid tokenId, string userId, Dictionary<string, object> formData = null)
        {
            // تکمیل وظیفه
            var isCompleted = await _taskManager.CompleteTaskAssignmentAsync(tokenId, userId, formData);
            
            if (!isCompleted)
            {
                throw new InvalidOperationException($"Failed to complete task with token ID {tokenId}");
            }

            // دریافت نمونه فرآیند مرتبط با توکن
            var instance = await _processManager.GetProcessInstanceByTokenAsync(tokenId);
            if (instance == null)
            {
                throw new InvalidOperationException($"Process instance for token {tokenId} not found");
            }
            
            // با توجه به اینکه وظیفه تکمیل شده، ممکن است فرآیند نیز ادامه یافته باشد
            // بنابراین پردازنده را دریافت می‌کنیم و فرآیند را ادامه می‌دهیم
            var executor = await _processManager.GetExecutorForTokenAsync(tokenId);
            var updatedInstance = await executor.StartProcessAsync();
            
            // ذخیره وضعیت به‌روز شده
            await _processManager.SaveProcessInstanceAsync(updatedInstance);
            
            // برگرداندن جزئیات
            return updatedInstance;
        }
        
        #endregion
    }

    /// <summary>
    /// جزئیات یک نمونه فرآیند
    /// </summary>
 
} 