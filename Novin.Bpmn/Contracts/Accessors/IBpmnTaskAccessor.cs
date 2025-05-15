using Novin.Bpmn.V3.UserTasks;

namespace Novin.Bpmn.Contracts.Accessors
{
    /// <summary>
    /// رابط دسترسی به وظایف کاربری BPMN
    /// </summary>
    public interface IBpmnTaskAccessor
    {
        /// <summary>
        /// ذخیره یک وظیفه کاربری جدید
        /// </summary>
        Task SaveTaskAsync(BpmnV3UserTaskAssignment task);
        
        /// <summary>
        /// دریافت یک وظیفه کاربری با شناسه توکن
        /// </summary>
        Task<BpmnV3UserTaskAssignment> GetTaskByTokenAsync(Guid tokenId);
        
        /// <summary>
        /// دریافت همه وظایف کاربری فعال
        /// </summary>
        Task<List<BpmnV3UserTaskAssignment>> GetAllActiveTasksAsync();
        
        /// <summary>
        /// دریافت وظایف کاربری قابل انجام توسط یک کاربر
        /// </summary>
        Task<List<BpmnV3UserTaskAssignment>> GetTasksByUserAsync(string userId, List<string> userGroups = null);
        
        /// <summary>
        /// دریافت وظایف کاربری یک فرآیند
        /// </summary>
        Task<List<BpmnV3UserTaskAssignment>> GetTasksByProcessInstanceAsync(string processInstanceId);
        
        /// <summary>
        /// دریافت وظایف کاربری یک المان
        /// </summary>
        Task<List<BpmnV3UserTaskAssignment>> GetTasksByElementIdAsync(string elementId);
        
        /// <summary>
        /// دریافت وظایف کاربری یک گروه
        /// </summary>
        Task<List<BpmnV3UserTaskAssignment>> GetTasksByGroupAsync(string groupId);
        
        /// <summary>
        /// به‌روزرسانی وضعیت یک وظیفه کاربری
        /// </summary>
        Task UpdateTaskStatusAsync(Guid tokenId, UserTaskStatus status);
        
        /// <summary>
        /// تخصیص یک وظیفه کاربری به کاربر
        /// </summary>
        Task AssignTaskToUserAsync(Guid tokenId, string userId);
        
        /// <summary>
        /// ثبت تکمیل یک وظیفه کاربری
        /// </summary>
        Task CompleteTaskAsync(Guid tokenId, string userId, Dictionary<string, object> formData = null);
        
        /// <summary>
        /// حذف یک وظیفه کاربری
        /// </summary>
        Task DeleteTaskAsync(Guid tokenId);
        
        /// <summary>
        /// دریافت آمار وظایف کاربری
        /// </summary>
        Task<TaskStatistics> GetTaskStatisticsAsync();
    }
    
    /// <summary>
    /// آمار وظایف کاربری
    /// </summary>
    public class TaskStatistics
    {
        /// <summary>
        /// تعداد کل وظایف
        /// </summary>
        public int TotalTasks { get; set; }
        
        /// <summary>
        /// تعداد وظایف منتظر
        /// </summary>
        public int PendingTasks { get; set; }
        
        /// <summary>
        /// تعداد وظایف تخصیص داده شده
        /// </summary>
        public int ClaimedTasks { get; set; }
        
        /// <summary>
        /// تعداد وظایف تکمیل شده
        /// </summary>
        public int CompletedTasks { get; set; }
        
        /// <summary>
        /// تعداد وظایف لغو شده
        /// </summary>
        public int CanceledTasks { get; set; }
        
        /// <summary>
        /// آمار وظایف به تفکیک کاربر
        /// </summary>
        public Dictionary<string, int> TasksByUser { get; set; } = new Dictionary<string, int>();
        
        /// <summary>
        /// آمار وظایف به تفکیک گروه
        /// </summary>
        public Dictionary<string, int> TasksByGroup { get; set; } = new Dictionary<string, int>();
        
        /// <summary>
        /// آمار وظایف به تفکیک فرآیند
        /// </summary>
        public Dictionary<string, int> TasksByProcess { get; set; } = new Dictionary<string, int>();
    }
} 