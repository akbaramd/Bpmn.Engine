using System.Collections.Concurrent;
using Novin.Bpmn.Contracts.Accessors;
using Novin.Bpmn.V3.UserTasks;

namespace Novin.Bpmn.Core.Accessors
{
    /// <summary>
    /// پیاده‌سازی دسترسی به وظایف کاربری BPMN در حافظه
    /// </summary>
    public class InMemoryBpmnTaskAccessor : IBpmnTaskAccessor
    {
        // ذخیره وظایف با کلید شناسه توکن آنها
        private readonly ConcurrentDictionary<Guid, BpmnV3UserTaskAssignment> _tasks = new ConcurrentDictionary<Guid, BpmnV3UserTaskAssignment>();
        
        // نگاشت‌های جستجو برای دسترسی سریع‌تر
        private readonly ConcurrentDictionary<string, HashSet<Guid>> _tasksByElementId = new ConcurrentDictionary<string, HashSet<Guid>>();
        private readonly ConcurrentDictionary<string, HashSet<Guid>> _tasksByAssignee = new ConcurrentDictionary<string, HashSet<Guid>>();
        private readonly ConcurrentDictionary<string, HashSet<Guid>> _tasksByGroup = new ConcurrentDictionary<string, HashSet<Guid>>();
        
        /// <summary>
        /// ذخیره یک وظیفه کاربری جدید
        /// </summary>
        public Task SaveTaskAsync(BpmnV3UserTaskAssignment task)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            
            _tasks[task.TokenId] = task;
            
            // به‌روزرسانی نگاشت‌های جستجو
            UpdateLookupMaps(task);
            
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// دریافت یک وظیفه کاربری با شناسه توکن
        /// </summary>
        public Task<BpmnV3UserTaskAssignment> GetTaskByTokenAsync(Guid tokenId)
        {
            _tasks.TryGetValue(tokenId, out var task);
            return Task.FromResult(task);
        }
        
        /// <summary>
        /// دریافت همه وظایف کاربری فعال
        /// </summary>
        public Task<List<BpmnV3UserTaskAssignment>> GetAllActiveTasksAsync()
        {
            var activeTasks = _tasks.Values
                .Where(t => t.Status == UserTaskStatus.Created || t.Status == UserTaskStatus.Claimed)
                .ToList();
                
            return Task.FromResult(activeTasks);
        }
        
        /// <summary>
        /// دریافت وظایف کاربری قابل انجام توسط یک کاربر
        /// </summary>
        public Task<List<BpmnV3UserTaskAssignment>> GetTasksByUserAsync(string userId, List<string> userGroups = null)
        {
            if (string.IsNullOrEmpty(userId)) throw new ArgumentNullException(nameof(userId));
            
            var tasks = new List<BpmnV3UserTaskAssignment>();
            
            // وظایف مستقیماً اختصاص داده شده به کاربر
            if (_tasksByAssignee.TryGetValue(userId, out var userTaskIds))
            {
                foreach (var tokenId in userTaskIds)
                {
                    if (_tasks.TryGetValue(tokenId, out var task) && 
                        (task.Status == UserTaskStatus.Created || task.Status == UserTaskStatus.Claimed))
                    {
                        tasks.Add(task);
                    }
                }
            }
            
            // وظایف قابل انجام توسط کاربر با توجه به گروه‌های آن
            if (userGroups != null && userGroups.Count > 0)
            {
                foreach (var group in userGroups)
                {
                    if (_tasksByGroup.TryGetValue(group, out var groupTaskIds))
                    {
                        foreach (var tokenId in groupTaskIds)
                        {
                            if (_tasks.TryGetValue(tokenId, out var task) && 
                                !tasks.Contains(task) &&
                                (task.Status == UserTaskStatus.Created))
                            {
                                tasks.Add(task);
                            }
                        }
                    }
                }
            }
            
            // وظایف بدون تخصیص که همه کاربران می‌توانند انجام دهند
            var unassignedTasks = _tasks.Values
                .Where(t => t.Status == UserTaskStatus.Created && 
                           string.IsNullOrEmpty(t.Assignee) && 
                           !t.CandidateUsers.Any() && 
                           !t.CandidateGroups.Any() &&
                           !tasks.Contains(t))
                .ToList();
                
            tasks.AddRange(unassignedTasks);
            
            return Task.FromResult(tasks);
        }
        
        /// <summary>
        /// دریافت وظایف کاربری یک فرآیند
        /// </summary>
        public Task<List<BpmnV3UserTaskAssignment>> GetTasksByProcessInstanceAsync(string processInstanceId)
        {
            if (string.IsNullOrEmpty(processInstanceId)) throw new ArgumentNullException(nameof(processInstanceId));
            
            var tasks = _tasks.Values
                .Where(t => t.ProcessInstanceId == processInstanceId)
                .ToList();
                
            return Task.FromResult(tasks);
        }
        
        /// <summary>
        /// دریافت وظایف کاربری یک المان
        /// </summary>
        public Task<List<BpmnV3UserTaskAssignment>> GetTasksByElementIdAsync(string elementId)
        {
            if (string.IsNullOrEmpty(elementId)) throw new ArgumentNullException(nameof(elementId));
            
            var tasks = new List<BpmnV3UserTaskAssignment>();
            
            if (_tasksByElementId.TryGetValue(elementId, out var taskIds))
            {
                foreach (var tokenId in taskIds)
                {
                    if (_tasks.TryGetValue(tokenId, out var task))
                    {
                        tasks.Add(task);
                    }
                }
            }
            
            return Task.FromResult(tasks);
        }
        
        /// <summary>
        /// دریافت وظایف کاربری یک گروه
        /// </summary>
        public Task<List<BpmnV3UserTaskAssignment>> GetTasksByGroupAsync(string groupId)
        {
            if (string.IsNullOrEmpty(groupId)) throw new ArgumentNullException(nameof(groupId));
            
            var tasks = new List<BpmnV3UserTaskAssignment>();
            
            if (_tasksByGroup.TryGetValue(groupId, out var taskIds))
            {
                foreach (var tokenId in taskIds)
                {
                    if (_tasks.TryGetValue(tokenId, out var task) && task.Status == UserTaskStatus.Created)
                    {
                        tasks.Add(task);
                    }
                }
            }
            
            return Task.FromResult(tasks);
        }
        
        /// <summary>
        /// به‌روزرسانی وضعیت یک وظیفه کاربری
        /// </summary>
        public async Task UpdateTaskStatusAsync(Guid tokenId, UserTaskStatus status)
        {
            if (!_tasks.TryGetValue(tokenId, out var task))
            {
                throw new InvalidOperationException($"هیچ وظیفه کاربری با توکن {tokenId} یافت نشد.");
            }
            
            task.Status = status;
            
            // اگر وظیفه تکمیل یا لغو شده، تاریخ را به‌روز کنیم
            if (status == UserTaskStatus.Completed && task.CompletedAt == null)
            {
                task.CompletedAt = DateTime.UtcNow;
            }
            
            await SaveTaskAsync(task);
        }
        
        /// <summary>
        /// تخصیص یک وظیفه کاربری به کاربر
        /// </summary>
        public async Task AssignTaskToUserAsync(Guid tokenId, string userId)
        {
            if (string.IsNullOrEmpty(userId)) throw new ArgumentNullException(nameof(userId));
            
            if (!_tasks.TryGetValue(tokenId, out var task))
            {
                throw new InvalidOperationException($"هیچ وظیفه کاربری با توکن {tokenId} یافت نشد.");
            }
            
            // حذف اختصاص قبلی
            if (!string.IsNullOrEmpty(task.Assignee) && _tasksByAssignee.TryGetValue(task.Assignee, out var oldAssigneeTasks))
            {
                oldAssigneeTasks.Remove(tokenId);
            }
            
            // ثبت اختصاص جدید
            task.Assignee = userId;
            task.Status = UserTaskStatus.Claimed;
            
            await SaveTaskAsync(task);
        }
        
        /// <summary>
        /// ثبت تکمیل یک وظیفه کاربری
        /// </summary>
        public async Task CompleteTaskAsync(Guid tokenId, string userId, Dictionary<string, object> formData = null)
        {
            if (string.IsNullOrEmpty(userId)) throw new ArgumentNullException(nameof(userId));
            
            if (!_tasks.TryGetValue(tokenId, out var task))
            {
                throw new InvalidOperationException($"هیچ وظیفه کاربری با توکن {tokenId} یافت نشد.");
            }
            
            // بررسی مجوز کاربر برای تکمیل وظیفه
            if (!task.CanCompleteTask(userId))
            {
                throw new UnauthorizedAccessException($"کاربر {userId} مجوز تکمیل این وظیفه را ندارد.");
            }
            
            task.CompleteTask(userId, formData);
            await SaveTaskAsync(task);
        }
        
        /// <summary>
        /// حذف یک وظیفه کاربری
        /// </summary>
        public Task DeleteTaskAsync(Guid tokenId)
        {
            if (_tasks.TryRemove(tokenId, out var task))
            {
                // حذف از نگاشت‌های جستجو
                if (!string.IsNullOrEmpty(task.TaskElementId) && _tasksByElementId.TryGetValue(task.TaskElementId, out var elementTasks))
                {
                    elementTasks.Remove(tokenId);
                }
                
                if (!string.IsNullOrEmpty(task.Assignee) && _tasksByAssignee.TryGetValue(task.Assignee, out var assigneeTasks))
                {
                    assigneeTasks.Remove(tokenId);
                }
                
                foreach (var group in task.CandidateGroups)
                {
                    if (_tasksByGroup.TryGetValue(group, out var groupTasks))
                    {
                        groupTasks.Remove(tokenId);
                    }
                }
            }
            
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// دریافت آمار وظایف کاربری
        /// </summary>
        public Task<TaskStatistics> GetTaskStatisticsAsync()
        {
            var stats = new TaskStatistics
            {
                TotalTasks = _tasks.Count,
                PendingTasks = _tasks.Values.Count(t => t.Status == UserTaskStatus.Created),
                ClaimedTasks = _tasks.Values.Count(t => t.Status == UserTaskStatus.Claimed),
                CompletedTasks = _tasks.Values.Count(t => t.Status == UserTaskStatus.Completed),
                CanceledTasks = _tasks.Values.Count(t => t.Status == UserTaskStatus.Canceled)
            };
            
            // آمار به تفکیک کاربر
            foreach (var task in _tasks.Values.Where(t => !string.IsNullOrEmpty(t.Assignee)))
            {
                if (!stats.TasksByUser.ContainsKey(task.Assignee))
                {
                    stats.TasksByUser[task.Assignee] = 0;
                }
                
                stats.TasksByUser[task.Assignee]++;
            }
            
            // آمار به تفکیک گروه
            foreach (var task in _tasks.Values)
            {
                foreach (var group in task.CandidateGroups)
                {
                    if (!stats.TasksByGroup.ContainsKey(group))
                    {
                        stats.TasksByGroup[group] = 0;
                    }
                    
                    stats.TasksByGroup[group]++;
                }
            }
            
            return Task.FromResult(stats);
        }
        
        /// <summary>
        /// به‌روزرسانی نگاشت‌های جستجو برای دسترسی سریع‌تر
        /// </summary>
        private void UpdateLookupMaps(BpmnV3UserTaskAssignment task)
        {
            // نگاشت المان
            if (!string.IsNullOrEmpty(task.TaskElementId))
            {
                _tasksByElementId.AddOrUpdate(
                    task.TaskElementId, 
                    new HashSet<Guid> { task.TokenId },
                    (key, existingSet) => 
                    {
                        existingSet.Add(task.TokenId);
                        return existingSet;
                    }
                );
            }
            
            // نگاشت کاربر تخصیص یافته
            if (!string.IsNullOrEmpty(task.Assignee))
            {
                _tasksByAssignee.AddOrUpdate(
                    task.Assignee, 
                    new HashSet<Guid> { task.TokenId },
                    (key, existingSet) => 
                    {
                        existingSet.Add(task.TokenId);
                        return existingSet;
                    }
                );
            }
            
            // نگاشت گروه‌ها
            foreach (var group in task.CandidateGroups)
            {
                _tasksByGroup.AddOrUpdate(
                    group, 
                    new HashSet<Guid> { task.TokenId },
                    (key, existingSet) => 
                    {
                        existingSet.Add(task.TokenId);
                        return existingSet;
                    }
                );
            }
        }

        /// <summary>
        /// دریافت یک موجودیت با کلید آن - پیاده‌سازی IBpmnDataAccessorBase
        /// </summary>
        public Task<BpmnV3UserTaskAssignment> GetByIdAsync(Guid id)
        {
            return GetTaskByTokenAsync(id);
        }
        
        /// <summary>
        /// ذخیره یک موجودیت جدید یا به‌روزرسانی موجودیت موجود - پیاده‌سازی IBpmnDataAccessorBase
        /// </summary>
        public Task SaveAsync(BpmnV3UserTaskAssignment entity)
        {
            return SaveTaskAsync(entity);
        }
        
        /// <summary>
        /// حذف یک موجودیت با کلید آن - پیاده‌سازی IBpmnDataAccessorBase
        /// </summary>
        public Task<bool> DeleteAsync(Guid id)
        {
            var success = _tasks.TryRemove(id, out _);
            return Task.FromResult(success);
        }
        
        /// <summary>
        /// دریافت تمام موجودیت‌ها - پیاده‌سازی IBpmnDataAccessorBase
        /// </summary>
        public Task<IEnumerable<BpmnV3UserTaskAssignment>> GetAllAsync()
        {
            return Task.FromResult<IEnumerable<BpmnV3UserTaskAssignment>>(_tasks.Values.ToList());
        }
        
        /// <summary>
        /// جستجو در موجودیت‌ها با استفاده از شرایط فیلتر - پیاده‌سازی IBpmnDataAccessorBase
        /// </summary>
        public Task<IEnumerable<BpmnV3UserTaskAssignment>> FindAsync(Func<BpmnV3UserTaskAssignment, bool> predicate)
        {
            var filteredTasks = _tasks.Values.Where(predicate).ToList();
            return Task.FromResult<IEnumerable<BpmnV3UserTaskAssignment>>(filteredTasks);
        }
    }
} 