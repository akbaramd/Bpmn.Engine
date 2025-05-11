using System;
using System.Collections.Generic;
using System.Linq;
using Novin.Bpmn.EventSourcing.Core.Models;
using Novin.Bpmn.EventSourcing.Events;

namespace Novin.Bpmn.EventSourcing.Core;

/// <summary>
/// وضعیت نمونه فرآیند BPMN
/// </summary>
public class BpmnProcessState
{
    /// <summary>
    /// شناسه نمونه فرآیند
    /// </summary>
    public required string ProcessInstanceId { get; set; }
    
    /// <summary>
    /// شناسه تعریف فرآیند
    /// </summary>
    public string? ProcessDefinitionId { get; set; }
    
    /// <summary>
    /// کلید انتشار
    /// </summary>
    public string? DeploymentKey { get; set; }
    
    /// <summary>
    /// وضعیت فرآیند
    /// </summary>
    public ProcessStatus Status { get; set; }
    
    /// <summary>
    /// المان‌های فعال
    /// Currently active elements
    /// </summary>
    public HashSet<string> ActiveElements { get; set; } = new();
    
    /// <summary>
    /// المان‌های تکمیل شده
    /// Completed elements
    /// </summary>
    public HashSet<string> CompletedElements { get; set; } = new();
    
    /// <summary>
    /// وضعیت المان‌ها
    /// Element statuses
    /// </summary>
    public Dictionary<string, ElementStatus> ElementStatuses { get; set; } = new();
    
    /// <summary>
    /// متغیرهای فرآیند
    /// </summary>
    public Dictionary<string, object> Variables { get; set; } = new();
    
    /// <summary>
    /// مسیرهای اجرای فرآیند
    /// Execution paths showing the flow between elements and sequence flows
    /// </summary>
    public List<ExecutionPath> ExecutionPaths { get; set; } = new();
    
    /// <summary>
    /// اجراهای فعال فعلی
    /// Current active executions by execution ID
    /// </summary>
    public Dictionary<string, ExecutionPath> ActiveExecutions { get; set; } = new();
    
    /// <summary>
    /// رکوردهای تراکنش‌های فرآیند
    /// Transaction records in the process
    /// </summary>
    public List<TransactionRecord> Transactions { get; set; } = new();
    
    /// <summary>
    /// تراکنش‌های فعال فعلی
    /// Current active transactions by transaction ID
    /// </summary>
    public Dictionary<string, TransactionRecord> ActiveTransactions { get; set; } = new();
    
    /// <summary>
    /// نگاشت رویدادها به مسیرهای اجرا
    /// Maps events to their execution paths
    /// </summary>
    public Dictionary<string, string> EventToExecutionPath { get; set; } = new();
    
    /// <summary>
    /// نگاشت المان‌ها به مسیرهای اجرا
    /// Maps elements to their execution paths
    /// </summary>
    public Dictionary<string, List<string>> ElementExecutionPaths { get; set; } = new();
    
    /// <summary>
    /// نگاشت المان‌ها به جریان‌های توالی
    /// Maps elements to their sequence flows
    /// </summary>
    public Dictionary<string, List<string>> ElementToSequenceFlows { get; set; } = new();
    
    /// <summary>
    /// اطلاعات وظایف فرآیند
    /// Task information for the process
    /// </summary>
    public Dictionary<string, TaskInfo> Tasks { get; set; } = new();
    
    /// <summary>
    /// شمارش اجرای المان‌ها
    /// Counts how many times an element is executed (useful for gateway merges)
    /// </summary>
    public Dictionary<string, int> ElementExecutionCounts { get; set; } = new();
    
    /// <summary>
    /// وضعیت‌های ادغام دروازه‌ها
    /// Gateway merge states - tracks how many incoming branches have been received
    /// </summary>
    public Dictionary<string, GatewayMergeInfo> GatewayMergeStates { get; set; } = new();
    
    /// <summary>
    /// آمار اجراها
    /// Execution statistics
    /// </summary>
    public ExecutionStatistics ExecutionStats { get; set; } = new();
    
    /// <summary>
    /// اضافه کردن رویداد به مسیر اجرا
    /// Add an event to an execution path
    /// </summary>
    /// <param name="executionId">ID of the execution path</param>
    /// <param name="bpmnEvent">The event to add</param>
    /// <returns>True if the event was added, false otherwise</returns>
    public bool AddEventToExecution(string executionId, BpmnEvent bpmnEvent)
    {
        // Find the execution path
        ExecutionPath? executionPath = null;
        if (ActiveExecutions.TryGetValue(executionId, out executionPath))
        {
            // Add event to active execution
            executionPath.AddEvent(bpmnEvent);
            
            // Map event to execution path
            EventToExecutionPath[bpmnEvent.EventId.ToString()] = executionId;
            
            return true;
        }
        else
        {
            // Look in all execution paths
            executionPath = ExecutionPaths.FirstOrDefault(e => e.ExecutionId == executionId);
            if (executionPath != null)
            {
                // Add event to execution
                executionPath.AddEvent(bpmnEvent);
                
                // Map event to execution path
                EventToExecutionPath[bpmnEvent.EventId.ToString()] = executionId;
                
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// بروزرسانی آمار اجراها
    /// Update execution statistics
    /// </summary>
    public void UpdateExecutionStatistics()
    {
        ExecutionStats.TotalExecutionPaths = ExecutionPaths.Count;
        ExecutionStats.ActiveExecutionPaths = ActiveExecutions.Count;
        ExecutionStats.CompletedExecutionPaths = ExecutionPaths.Count(e => e.Status == ExecutionStatus.Completed);
        ExecutionStats.FailedExecutionPaths = ExecutionPaths.Count(e => e.Status == ExecutionStatus.Failed);
        ExecutionStats.WaitingExecutionPaths = ExecutionPaths.Count(e => e.Status == ExecutionStatus.Waiting);
        
        // Calculate average events per execution
        if (ExecutionPaths.Count > 0)
        {
            ExecutionStats.AverageEventsPerExecution = ExecutionPaths.Average(e => e.TotalEventCount);
        }
    }
}

/// <summary>
/// آمار اجراها
/// Statistics about execution paths
/// </summary>
public class ExecutionStatistics
{
    /// <summary>
    /// تعداد کل مسیرهای اجرا
    /// Total number of execution paths
    /// </summary>
    public int TotalExecutionPaths { get; set; }
    
    /// <summary>
    /// تعداد مسیرهای اجرای فعال
    /// Number of active execution paths
    /// </summary>
    public int ActiveExecutionPaths { get; set; }
    
    /// <summary>
    /// تعداد مسیرهای اجرای تکمیل شده
    /// Number of completed execution paths
    /// </summary>
    public int CompletedExecutionPaths { get; set; }
    
    /// <summary>
    /// تعداد مسیرهای اجرای ناموفق
    /// Number of failed execution paths
    /// </summary>
    public int FailedExecutionPaths { get; set; }
    
    /// <summary>
    /// تعداد مسیرهای اجرای در حال انتظار
    /// Number of waiting execution paths
    /// </summary>
    public int WaitingExecutionPaths { get; set; }
    
    /// <summary>
    /// میانگین رویدادها در هر مسیر اجرا
    /// Average events per execution path
    /// </summary>
    public double AverageEventsPerExecution { get; set; }
}

/// <summary>
/// وضعیت المان
/// Status of an element
/// </summary>
public class ElementStatus
{
    /// <summary>
    /// شناسه المان
    /// Element ID
    /// </summary>
    public string ElementId { get; set; } = null!;
    
    /// <summary>
    /// نوع المان
    /// Element type
    /// </summary>
    public string ElementType { get; set; } = null!;
    
    /// <summary>
    /// وضعیت المان
    /// Element status
    /// </summary>
    public string Status { get; set; } = "Created";
    
    /// <summary>
    /// زمان ایجاد
    /// Creation time
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// زمان به‌روزرسانی
    /// Last update time
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// زمان تکمیل
    /// Completion time
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// زمان شکست
    /// Failure time
    /// </summary>
    public DateTime? FailedAt { get; set; }
    
    /// <summary>
    /// پیام خطا در صورت شکست
    /// Error message if failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}
