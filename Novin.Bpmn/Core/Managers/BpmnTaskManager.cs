using Novin.Bpmn.Contracts.Accessors;
using Novin.Bpmn.Contracts.Managers;
using Novin.Bpmn.V3.UserTasks;

namespace Novin.Bpmn.Core.Managers
{
    /// <summary>
    /// مدیریت وظایف کاربری در موتور BPMN
    /// </summary>
    public class BpmnTaskManager : IBpmnTaskManager
    {
        private readonly IBpmnTaskAccessor _taskAccessor;
        
        // رویداد ایجاد وظیفه جدید
        public event EventHandler<BpmnV3UserTaskAssignment> OnUserTaskCreated;
        
        // رویداد تخصیص وظیفه
        public event EventHandler<BpmnV3UserTaskAssignment> OnUserTaskClaimed;
        
        // رویداد تکمیل وظیفه
        public event EventHandler<BpmnV3UserTaskAssignment> OnUserTaskCompleted;
        
        /// <summary>
        /// سازنده مدیریت کننده وظایف کاربری
        /// </summary>
        public BpmnTaskManager(IBpmnTaskAccessor taskAccessor)
        {
            _taskAccessor = taskAccessor ?? throw new ArgumentNullException(nameof(taskAccessor));
        }
        
        /// <summary>
        /// دریافت وظیفه کاربری با شناسه توکن
        /// </summary>
        public async Task<BpmnV3UserTaskAssignment> GetTaskAssignmentAsync(Guid tokenId)
        {
            return await _taskAccessor.GetTaskByTokenAsync(tokenId);
        }
        
        /// <summary>
        /// دریافت وظایف کاربری یک نمونه فرآیند
        /// </summary>
        public async Task<List<BpmnV3UserTaskAssignment>> GetTaskAssignmentsForProcessInstanceAsync(string processInstanceId)
        {
            return await _taskAccessor.GetTasksByProcessInstanceAsync(processInstanceId);
        }
        
        /// <summary>
        /// دریافت وظایف کاربری قابل انجام توسط یک کاربر
        /// </summary>
        public async Task<List<BpmnV3UserTaskAssignment>> GetAvailableTasksForUserAsync(string userId, List<string> userGroups = null)
        {
            return await _taskAccessor.GetTasksByUserAsync(userId, userGroups);
        }
        
        /// <summary>
        /// ایجاد یک وظیفه کاربری جدید
        /// </summary>
        public async Task<BpmnV3UserTaskAssignment> CreateTaskAssignmentAsync(Guid tokenId, string processInstanceId, string taskId, string taskName, string assignee = null, List<string> candidateUsers = null, List<string> candidateGroups = null)
        {
            var assignment = new BpmnV3UserTaskAssignment
            {
                TokenId = tokenId,
                TaskElementId = taskId,
                TaskName = taskName,
                Assignee = assignee,
                ProcessInstanceId = processInstanceId,
                CandidateUsers = candidateUsers ?? new List<string>(),
                CandidateGroups = candidateGroups ?? new List<string>()
            };
            
            await _taskAccessor.SaveTaskAsync(assignment);
            
            // اطلاع‌رسانی ایجاد وظیفه جدید
            OnUserTaskCreated?.Invoke(this, assignment);
            
            return assignment;
        }
        
        /// <summary>
        /// تکمیل یک وظیفه کاربری توسط یک کاربر
        /// </summary>
        public async Task<bool> CompleteTaskAssignmentAsync(Guid tokenId, string userId, Dictionary<string, object> variables = null)
        {
            var task = await _taskAccessor.GetTaskByTokenAsync(tokenId);
            if (task == null)
            {
                throw new InvalidOperationException($"هیچ وظیفه کاربری با توکن {tokenId} یافت نشد.");
            }
            
            // بررسی آیا کاربر می‌تواند این وظیفه را تکمیل کند
            if (!task.CanCompleteTask(userId))
            {
                throw new UnauthorizedAccessException($"کاربر {userId} مجوز تکمیل این وظیفه را ندارد.");
            }
            
            // تکمیل وظیفه
            await _taskAccessor.CompleteTaskAsync(tokenId, userId, variables);
            
            // به‌روزرسانی وظیفه
            task = await _taskAccessor.GetTaskByTokenAsync(tokenId);
            
            // اطلاع‌رسانی تکمیل وظیفه
            OnUserTaskCompleted?.Invoke(this, task);
            
            return true;
        }
        
        /// <summary>
        /// تخصیص یک وظیفه کاربری به یک کاربر خاص
        /// </summary>
        public async Task<bool> ClaimTaskAssignmentAsync(Guid tokenId, string userId)
        {
            var task = await _taskAccessor.GetTaskByTokenAsync(tokenId);
            if (task == null)
            {
                throw new InvalidOperationException($"هیچ وظیفه کاربری با توکن {tokenId} یافت نشد.");
            }
            
            await _taskAccessor.AssignTaskToUserAsync(tokenId, userId);
            
            // به‌روزرسانی وظیفه
            task = await _taskAccessor.GetTaskByTokenAsync(tokenId);
            
            // اطلاع‌رسانی تخصیص وظیفه
            OnUserTaskClaimed?.Invoke(this, task);
            
            return true;
        }
        
        /// <summary>
        /// حذف وظیفه کاربری
        /// </summary>
        public async Task DeleteTaskAssignmentAsync(Guid tokenId)
        {
            await _taskAccessor.DeleteTaskAsync(tokenId);
        }
        
        /// <summary>
        /// دریافت آمار وظایف کاربری
        /// </summary>
        public async Task<TaskStatistics> GetTaskStatisticsAsync()
        {
            return await _taskAccessor.GetTaskStatisticsAsync();
        }
    }
}