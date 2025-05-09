using Novin.Bpmn.V3.UserTasks;
using System;
using System.Collections.Generic;

namespace Novin.Bpmn.Core
{
    /// <summary>
    /// جزئیات یک نمونه فرآیند BPMN برای نمایش
    /// </summary>
    public class ProcessInstanceDetails
    {
        /// <summary>
        /// شناسه نمونه فرآیند
        /// </summary>
        public string Id { get; set; }
        
        /// <summary>
        /// کلید تعریف فرآیند
        /// </summary>
        public string DeploymentKey { get; set; }
        
        /// <summary>
        /// شناسه فرآیند
        /// </summary>
        public string ProcessId { get; set; }
        
        /// <summary>
        /// لیست نودهای فعال
        /// </summary>
        public List<string> ActiveNodes { get; set; } = new List<string>();
        
        /// <summary>
        /// لیست توکن‌های فعال
        /// </summary>
        public List<BpmnV3Token> ActiveTokens { get; set; } = new List<BpmnV3Token>();
        
        /// <summary>
        /// لیست توکن‌های در حال انتظار
        /// </summary>
        public List<BpmnV3Token> WaitingTokens { get; set; } = new List<BpmnV3Token>();
        public List<BpmnV3Token> CompletedTokens { get; set; } = new List<BpmnV3Token>();
        
        /// <summary>
        /// متغیرهای فرآیند
        /// </summary>
        public Dictionary<string, object> Variables { get; set; } = new Dictionary<string, object>();
        
        /// <summary>
        /// وظایف کاربری فعال
        /// </summary>
        public List<BpmnV3UserTaskAssignment> UserTasks { get; set; } = new List<BpmnV3UserTaskAssignment>();
        
        /// <summary>
        /// وضعیت فرآیند
        /// </summary>
        public ProcessInstanceStatus Status { get; set; } = ProcessInstanceStatus.Active;
        
        /// <summary>
        /// تاریخ شروع فرآیند
        /// </summary>
        public DateTime StartedAt { get; set; } = DateTime.Now;
        
        /// <summary>
        /// تاریخ آخرین بروزرسانی
        /// </summary>
        public DateTime? LastUpdatedAt { get; set; }
        
        /// <summary>
        /// تاریخ پایان
        /// </summary>
        public DateTime? EndedAt { get; set; }
    }
} 