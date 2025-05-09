using System;
using System.Collections.Generic;
using System.Linq;
using Novin.Bpmn.Contracts;
using Novin.Bpmn.Models;

namespace Novin.Bpmn.V3.UserTasks
{
    /// <summary>
    /// مدیریت تخصیص وظایف کاربری
    /// </summary>
    public class BpmnV3UserTaskAssignment : IBpmnEntity<Guid>
    {
        /// <summary>
        /// شناسه موجودیت
        /// </summary>
        public Guid Id => TokenId;
        
        // شناسه توکن مرتبط با این وظیفه
        public Guid TokenId { get; set; }
        
        // شناسه المان (وظیفه کاربری)
        public string TaskElementId { get; set; }
        
        // عنوان وظیفه
        public string TaskName { get; set; }
        
        // شرح وظیفه 
        public string TaskDescription { get; set; }
        
        // کاربر مسئول وظیفه (اگر به یک نفر اختصاص دارد)
        public string Assignee { get; set; }
        
        // گروه کاربران (اگر به گروهی از کاربران اختصاص دارد)
        public List<string> CandidateUsers { get; set; } = new List<string>();
        
        // گروه‌های کاربری که می‌توانند این وظیفه را انجام دهند
        public List<string> CandidateGroups { get; set; } = new List<string>();
        
        // شناسه نمونه فرآیند
        public string ProcessInstanceId { get; set; }
        
        // زمان ایجاد وظیفه
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // زمان مهلت انجام وظیفه (در صورت وجود)
        public DateTime? DueDate { get; set; }
        
        // زمان انجام وظیفه
        public DateTime? CompletedAt { get; set; }
        
        // کاربری که وظیفه را انجام داده است
        public string CompletedBy { get; set; }
        
        // وضعیت وظیفه
        public UserTaskStatus Status { get; set; } = UserTaskStatus.Created;
        
        // متغیرهای ورودی تکمیل وظیفه
        public Dictionary<string, object> FormData { get; set; } = new Dictionary<string, object>();
        
        /// <summary>
        /// ایجاد یک وظیفه کاربری جدید از روی تعریف BpmnUserTask
        /// </summary>
        public static BpmnV3UserTaskAssignment CreateFromDefinition(BpmnUserTask userTask, Guid tokenId, string processInstanceId = null)
        {
            var assignment = new BpmnV3UserTaskAssignment
            {
                TokenId = tokenId,
                TaskElementId = userTask.id,
                TaskName = userTask.name,
                TaskDescription = userTask.documentation?.FirstOrDefault()?.textFormat,
                ProcessInstanceId = processInstanceId
            };
            
            // استخراج اطلاعات تخصیص از CustomAttributes
            if (userTask.extensionElements?.Any != null)
            {
                foreach (var element in userTask.extensionElements.Any)
                {
                    if (element.LocalName == "assignee")
                    {
                        assignment.Assignee = element.InnerText;
                    }
                    else if (element.LocalName == "candidateUsers")
                    {
                        var users = element.InnerText.Split(',');
                        assignment.CandidateUsers.AddRange(users.Select(u => u.Trim()));
                    }
                    else if (element.LocalName == "candidateGroups")
                    {
                        var groups = element.InnerText.Split(',');
                        assignment.CandidateGroups.AddRange(groups.Select(g => g.Trim()));
                    }
                    else if (element.LocalName == "dueDate")
                    {
                        if (DateTime.TryParse(element.InnerText, out var dueDate))
                        {
                            assignment.DueDate = dueDate;
                        }
                    }
                }
            }
            
            return assignment;
        }
        
        /// <summary>
        /// بررسی می‌کند آیا کاربر با شناسه مشخص شده می‌تواند این وظیفه را انجام دهد
        /// </summary>
        public bool CanCompleteTask(string userId, List<string> userGroups = null)
        {
            // اگر وظیفه قبلاً تکمیل شده باشد
            if (Status != UserTaskStatus.Created)
            {
                return false;
            }
            
            // اگر به یک کاربر خاص اختصاص داده شده باشد
            if (!string.IsNullOrEmpty(Assignee))
            {
                return Assignee == userId;
            }
            
            // اگر کاربر در لیست کاندیداها باشد
            if (CandidateUsers.Contains(userId))
            {
                return true;
            }
            
            // اگر کاربر عضو یکی از گروه‌های کاندیدا باشد
            if (userGroups != null && CandidateGroups.Any(g => userGroups.Contains(g)))
            {
                return true;
            }
            
            // اگر هیچ تخصیصی وجود نداشته باشد، همه کاربران می‌توانند انجام دهند
            return !CandidateUsers.Any() && !CandidateGroups.Any() && string.IsNullOrEmpty(Assignee);
        }
        
        /// <summary>
        /// تکمیل وظیفه توسط کاربر مشخص شده
        /// </summary>
        public void CompleteTask(string userId, Dictionary<string, object> formData = null)
        {
            Status = UserTaskStatus.Completed;
            CompletedAt = DateTime.UtcNow;
            CompletedBy = userId;
            
            if (formData != null)
            {
                FormData = new Dictionary<string, object>(formData);
            }
        }
        
        /// <summary>
        /// انتساب وظیفه به کاربر مشخص
        /// </summary>
        public void ClaimTask(string userId)
        {
            if (Status != UserTaskStatus.Created)
            {
                throw new InvalidOperationException("وظیفه در وضعیتی نیست که بتوان آن را تخصیص داد.");
            }
            
            // بررسی آیا کاربر می‌تواند این وظیفه را تخصیص دهد
            if (!string.IsNullOrEmpty(Assignee) && Assignee != userId)
            {
                throw new InvalidOperationException("این وظیفه قبلاً به کاربر دیگری اختصاص یافته است.");
            }
            
            Assignee = userId;
            Status = UserTaskStatus.Claimed;
        }
    }
    
    /// <summary>
    /// وضعیت‌های ممکن برای یک وظیفه کاربری
    /// </summary>
    public enum UserTaskStatus
    {
        Created,    // وظیفه ایجاد شده اما هنوز تخصیص داده نشده است
        Claimed,    // وظیفه توسط یک کاربر تخصیص داده شده است
        Completed,  // وظیفه تکمیل شده است
        Canceled    // وظیفه لغو شده است
    }
}