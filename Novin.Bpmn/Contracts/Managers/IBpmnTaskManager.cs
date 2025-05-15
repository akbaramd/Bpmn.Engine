using Novin.Bpmn.Contracts.Accessors;
using Novin.Bpmn.V3.UserTasks;

namespace Novin.Bpmn.Contracts.Managers
{
    /// <summary>
    /// رابط مدیریت وظایف کاربری BPMN
    /// </summary>
    public interface IBpmnTaskManager
    {
        /// <summary>
        /// رویداد زمانی که یک وظیفه کاربری جدید ایجاد می‌شود
        /// </summary>
        event EventHandler<BpmnV3UserTaskAssignment> OnUserTaskCreated;
        
        /// <summary>
        /// رویداد زمانی که یک وظیفه کاربری به کاربر تخصیص داده می‌شود
        /// </summary>
        event EventHandler<BpmnV3UserTaskAssignment> OnUserTaskClaimed;
        
        /// <summary>
        /// رویداد زمانی که یک وظیفه کاربری تکمیل می‌شود
        /// </summary>
        event EventHandler<BpmnV3UserTaskAssignment> OnUserTaskCompleted;
        
        /// <summary>
        /// دریافت یک وظیفه کاربری با شناسه توکن
        /// </summary>
        Task<BpmnV3UserTaskAssignment> GetTaskAssignmentAsync(Guid tokenId);
        
        /// <summary>
        /// دریافت وظایف کاربری یک فرآیند
        /// </summary>
        Task<List<BpmnV3UserTaskAssignment>> GetTaskAssignmentsForProcessInstanceAsync(string processInstanceId);
        
        /// <summary>
        /// دریافت وظایف کاربری قابل انجام توسط یک کاربر
        /// </summary>
        Task<List<BpmnV3UserTaskAssignment>> GetAvailableTasksForUserAsync(string userId, List<string> userGroups = null);
        
        /// <summary>
        /// ایجاد یک وظیفه کاربری جدید
        /// </summary>
        Task<BpmnV3UserTaskAssignment> CreateTaskAssignmentAsync(Guid tokenId, string processInstanceId, string taskId, string taskName, string assignee = null, List<string> candidateUsers = null, List<string> candidateGroups = null);
        
        /// <summary>
        /// تکمیل یک وظیفه کاربری
        /// </summary>
        Task<bool> CompleteTaskAssignmentAsync(Guid tokenId, string userId, Dictionary<string, object> variables = null);
        
        /// <summary>
        /// تخصیص یک وظیفه کاربری به کاربر
        /// </summary>
        Task<bool> ClaimTaskAssignmentAsync(Guid tokenId, string userId);
        
        /// <summary>
        /// حذف یک وظیفه کاربری
        /// </summary>
        Task DeleteTaskAssignmentAsync(Guid tokenId);
        
        /// <summary>
        /// دریافت آمار وظایف کاربری
        /// </summary>
        Task<TaskStatistics> GetTaskStatisticsAsync();
    }
} 