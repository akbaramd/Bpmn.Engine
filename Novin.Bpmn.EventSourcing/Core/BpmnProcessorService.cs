using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Events;
using Novin.Bpmn.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Novin.Bpmn.EventSourcing.Core;

/// <summary>
/// سرویس پردازش و مدیریت فرآیندهای BPMN
/// این سرویس مسئول خواندن تعریف BPMN، مدیریت نمونه‌های فرآیند و اجرای فرآیند با استفاده از Event Sourcing است
/// </summary>
public class BpmnProcessorService
{
    private readonly IEventBus _eventBus;
    private readonly IStateStore _stateStore;
    private readonly IEventStore _eventStore;
    private readonly IBpmnDefinitionStore _definitionStore;
    private readonly IBpmnDefinitionStorage _definitionStorage;
    private readonly ILogger<BpmnProcessorService> _logger;
    
    /// <summary>
    /// ایجاد نمونه جدید از سرویس پردازش BPMN
    /// </summary>
    public BpmnProcessorService(
        IEventBus eventBus,
        IStateStore stateStore,
        IEventStore eventStore,
        IBpmnDefinitionStore definitionStore,
        IBpmnDefinitionStorage definitionStorage,
        ILogger<BpmnProcessorService> logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        _definitionStore = definitionStore ?? throw new ArgumentNullException(nameof(definitionStore));
        _definitionStorage = definitionStorage ?? throw new ArgumentNullException(nameof(definitionStorage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <summary>
    /// تبدیل محتوای XML به مدل تعریف BPMN
    /// </summary>
    public BpmnDefinitions ParseBpmnXml(string xmlContent)
    {
        if (string.IsNullOrEmpty(xmlContent))
            throw new ArgumentException("XML content cannot be empty", nameof(xmlContent));
            
        try
        {
            var serializer = new XmlSerializer(typeof(BpmnDefinitions));
            using var reader = new StringReader(xmlContent);
            var definitions = (BpmnDefinitions)serializer.Deserialize(reader);
            
            if (definitions == null)
                throw new InvalidOperationException("Failed to deserialize BPMN XML");
                
            _logger.LogInformation("Successfully parsed BPMN definition with ID {DefinitionId}", definitions.id);
            return definitions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing BPMN XML content");
            throw new BpmnProcessorException("Failed to parse BPMN XML", ex);
        }
    }
    
    /// <summary>
    /// ایجاد تعریف جدید BPMN در سیستم
    /// </summary>
    public async Task<string> DeployProcessDefinitionAsync(
        string deploymentKey, 
        string xmlContent,
        string label = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(deploymentKey))
            throw new ArgumentException("Deployment key cannot be empty", nameof(deploymentKey));
            
        if (string.IsNullOrEmpty(xmlContent))
            throw new ArgumentException("XML content cannot be empty", nameof(xmlContent));
            
        // تبدیل محتوای XML به مدل تعریف BPMN
        var definitions = ParseBpmnXml(xmlContent);
        
        // ذخیره تعریف فرآیند در مخزن تعاریف
        var definitionId = await _definitionStore.SaveDefinitionAsync(
            deploymentKey, 
            xmlContent, 
            definitions, 
            label, 
            cancellationToken);
        
        _logger.LogInformation("Deployed BPMN process definition with key {DeploymentKey} and ID {DefinitionId}", 
            deploymentKey, definitionId);
            
        return definitionId;
    }
    
    /// <summary>
    /// شروع یک نمونه جدید از فرآیند
    /// </summary>
    public async Task<string> StartProcessInstanceAsync(
        string deploymentKey,
        string processId = null,
        Dictionary<string, object> variables = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(deploymentKey))
            throw new ArgumentException("Deployment key cannot be empty", nameof(deploymentKey));
            
        // بررسی وجود تعریف فرآیند در حافظه
        var deploymentInfo = _definitionStorage.GetDeploymentInfo(deploymentKey);
        
        // اگر در حافظه نبود، از مخزن بازیابی می‌کنیم
        if (deploymentInfo == null)
        {
            deploymentInfo = await _definitionStore.GetDeploymentInfoAsync(deploymentKey, cancellationToken);
            
            if (deploymentInfo == null)
                throw new BpmnProcessorException($"Process definition with key {deploymentKey} not found");
                
            // بازیابی تعریف پارس شده از مخزن
            var parsedDefinition = await _definitionStore.GetParsedDefinitionAsync(
                deploymentKey, 
                ParseBpmnXml, 
                cancellationToken);
                
            // افزودن به حافظه برای دسترسی سریع‌تر در آینده
            _definitionStorage.AddDefinition(deploymentKey, deploymentInfo, parsedDefinition);
        }
        
        // بازیابی تعریف پارس شده از حافظه
        var definitions = _definitionStorage.GetParsedDefinition(deploymentKey);
        if (definitions == null)
            throw new BpmnProcessorException($"Failed to load BPMN definition for key {deploymentKey}");
        
        var process = FindProcess(definitions, processId);
        if (process == null)
            throw new BpmnProcessorException($"Process ID {processId ?? "<default>"} not found in definition {deploymentKey}");
            
        // ایجاد شناسه نمونه فرآیند
        var processInstanceId = Guid.NewGuid().ToString();
        
        // آماده‌سازی متغیرهای اولیه
        Dictionary<string, object> initialVariables = variables != null 
            ? new Dictionary<string, object>(variables) 
            : new Dictionary<string, object>();
        
        try 
        {
            // Create initial state
            var initialState = new BpmnProcessState
            {
                ProcessInstanceId = processInstanceId,
                ProcessDefinitionId = process.id,
                DeploymentKey = deploymentKey,
                ActiveElements = new HashSet<string>(),
                CompletedElements = new HashSet<string>(),
                Variables = initialVariables,
                Status = ProcessStatus.Created
            };

            // Save initial state
            await _stateStore.SaveStateAsync(processInstanceId, initialState, 0);
            
            // انتشار رویداد ایجاد نمونه فرآیند
            await _eventBus.PublishAsync(new ProcessInstanceCreated
            {
                ProcessInstanceId = processInstanceId,
                ProcessDefinitionId = process.id,
                ProcessDefinitionKey = deploymentKey,
                Variables = initialVariables
            }, cancellationToken);
            
            _logger.LogInformation("Started process instance {ProcessInstanceId} for definition {DeploymentKey}", 
                processInstanceId, deploymentKey);
                
            return processInstanceId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting process instance for deployment {DeploymentKey}", deploymentKey);
            
            // انتشار رویداد خطا در صورت بروز مشکل
            try 
            {
                await _eventBus.PublishAsync(new ProcessInstanceFailed
                {
                    ProcessInstanceId = processInstanceId,
                    ErrorMessage = ex.Message,
                    ErrorDetails = ex.ToString()
                }, CancellationToken.None);
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Error publishing process failure event");
            }
            
            throw new BpmnProcessorException($"Failed to start process instance: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// دریافت وضعیت نمونه فرآیند
    /// </summary>
    public async Task<BpmnProcessState> GetProcessInstanceStateAsync(
        string processInstanceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(processInstanceId))
            throw new ArgumentException("Process instance ID cannot be empty", nameof(processInstanceId));
            
        var state = await _stateStore.GetStateAsync<BpmnProcessState>(
            processInstanceId, cancellationToken);
            
        if (state == null)
            throw new BpmnProcessorException($"Process instance {processInstanceId} not found");
            
        return state;
    }
    
    /// <summary>
    /// دریافت تمام وظایف کاربری فعال برای یک نمونه فرآیند
    /// </summary>
    public async Task<Dictionary<string, TaskInfo>> GetUserTasksForProcessInstanceAsync(
        string processInstanceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(processInstanceId))
            throw new ArgumentException("Process instance ID cannot be empty", nameof(processInstanceId));
            
        var state = await GetProcessInstanceStateAsync(processInstanceId, cancellationToken);
        
        if (state.Tasks == null || !state.Tasks.Any())
            return new Dictionary<string, TaskInfo>();
            
        return state.Tasks
            .Where(t => t.Value.TaskType == "UserTask" && t.Value.Status == TaskStatus.Active)
            .ToDictionary(t => t.Key, t => t.Value);
    }
    
    /// <summary>
    /// دریافت وظایف کاربری یک کاربر
    /// </summary>
    public async Task<Dictionary<string, Dictionary<string, TaskInfo>>> GetUserTasksForUserAsync(
        string userId,
        ICollection<string> userGroups = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
            
        // پیدا کردن فرآیندهای فعال
        var activeProcesses = await _stateStore.FindStatesByPatternAsync<BpmnProcessState>(
            "*", // همه فرآیندهای ذخیره شده
            state => state.Status == ProcessStatus.Running, 
            cancellationToken);
            
        var result = new Dictionary<string, Dictionary<string, TaskInfo>>();
        
        foreach (var process in activeProcesses)
        {
            var userTasks = await GetUserTasksForProcessInstanceAsync(process.ProcessInstanceId, cancellationToken);
            
            // فیلتر کردن وظایف برای کاربر مشخص
            var userSpecificTasks = userTasks
                .Where(t => 
                    // تخصیص داده شده به این کاربر
                    (t.Value.Assignee == userId) ||
                    // بدون تخصیص ولی کاربر در لیست کاندیدا
                    (string.IsNullOrEmpty(t.Value.Assignee) && 
                     t.Value.CandidateUsers != null && 
                     t.Value.CandidateUsers.Contains(userId)) ||
                    // بدون تخصیص ولی کاربر عضو یکی از گروه‌های کاندیدا
                    (string.IsNullOrEmpty(t.Value.Assignee) && 
                     userGroups != null && 
                     t.Value.CandidateGroups != null && 
                     t.Value.CandidateGroups.Intersect(userGroups).Any()))
                .ToDictionary(t => t.Key, t => t.Value);
                
            if (userSpecificTasks.Any())
            {
                result[process.ProcessInstanceId] = userSpecificTasks;
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// تخصیص یک وظیفه کاربری به کاربر
    /// </summary>
    public async Task<TaskInfo> ClaimUserTaskAsync(
        string processInstanceId,
        string taskId,
        string userId,
        string userName = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(processInstanceId))
            throw new ArgumentException("Process instance ID cannot be empty", nameof(processInstanceId));
            
        if (string.IsNullOrEmpty(taskId))
            throw new ArgumentException("Task ID cannot be empty", nameof(taskId));
            
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
            
        var state = await GetProcessInstanceStateAsync(processInstanceId, cancellationToken);
        
        // بررسی وجود وظیفه در عناصر فعال
        if (!state.ActiveElements.Contains(taskId))
            throw new BpmnProcessorException($"Task {taskId} is not active in process instance {processInstanceId}");
            
        // بررسی اطلاعات وظیفه
        if (state.Tasks == null || !state.Tasks.TryGetValue(taskId, out var taskInfo))
            throw new BpmnProcessorException($"Task information for {taskId} not found in process instance {processInstanceId}");
            
        if (taskInfo.TaskType != "UserTask")
            throw new BpmnProcessorException($"Element {taskId} is not a user task");
            
        // بررسی وضعیت تخصیص وظیفه
        if (!string.IsNullOrEmpty(taskInfo.Assignee) && taskInfo.Assignee != userId)
            throw new BpmnProcessorException($"Task {taskId} is already assigned to {taskInfo.Assignee}");
            
        // اگر وظیفه قبلاً به همین کاربر تخصیص داده شده، کاری انجام نمی‌دهیم
        if (taskInfo.Assignee == userId)
            return taskInfo;
            
        try
        {
            // انتشار رویداد تخصیص وظیفه
            await _eventBus.PublishAsync(new Events.UserTaskClaimedEvent
            {
                ProcessInstanceId = processInstanceId,
                UserTaskId = taskId,
                AssigneeId = userId,
                AssigneeName = userName ?? userId
            }, cancellationToken);
            
            // کمی صبر برای پردازش رویداد
            await Task.Delay(50, cancellationToken);
            
            // دریافت وضعیت به‌روز شده
            state = await GetProcessInstanceStateAsync(processInstanceId, cancellationToken);
            
            if (state.Tasks != null && state.Tasks.TryGetValue(taskId, out var updatedTaskInfo))
                return updatedTaskInfo;
                
            throw new BpmnProcessorException($"Failed to update task assignment information");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error claiming user task {TaskId} in process {ProcessId}", taskId, processInstanceId);
            throw new BpmnProcessorException($"Failed to claim task: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// تکمیل یک وظیفه کاربری
    /// </summary>
    public async Task CompleteUserTaskAsync(
        string processInstanceId, 
        string taskId,
        Dictionary<string, object> formData = null,
        string userId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(processInstanceId))
            throw new ArgumentException("Process instance ID cannot be empty", nameof(processInstanceId));
            
        if (string.IsNullOrEmpty(taskId))
            throw new ArgumentException("Task ID cannot be empty", nameof(taskId));
            
        var (state, version) = await _stateStore.GetStateWithVersionAsync<BpmnProcessState>(processInstanceId, cancellationToken);
        if (state == null)
            throw new BpmnProcessorException($"Process instance {processInstanceId} not found");
        
        // بررسی وجود وظیفه در عناصر فعال
        if (!state.ActiveElements.Contains(taskId))
            throw new BpmnProcessorException($"Task {taskId} is not active in process instance {processInstanceId}");
            
        // بررسی اطلاعات وظیفه
        if (state.Tasks == null || !state.Tasks.TryGetValue(taskId, out var taskInfo))
            throw new BpmnProcessorException($"Task information for {taskId} not found in process instance {processInstanceId}");
        
        if (taskInfo.TaskType != "UserTask")
            throw new BpmnProcessorException($"Element {taskId} is not a user task");
            
        // بررسی وضعیت تخصیص
        if (!string.IsNullOrEmpty(taskInfo.Assignee) && userId != null && taskInfo.Assignee != userId)
            throw new BpmnProcessorException($"Task {taskId} is assigned to {taskInfo.Assignee}, not to {userId}");
            
        try
        {
            // آماده‌سازی داده‌های فرم
            Dictionary<string, object> finalFormData = formData != null
                ? new Dictionary<string, object>(formData)
                : new Dictionary<string, object>();

            // Update state
            state.Variables = state.Variables ?? new Dictionary<string, object>();
            foreach (var kvp in finalFormData)
            {
                state.Variables[kvp.Key] = kvp.Value;
            }

            // Remove from active elements
            state.ActiveElements.Remove(taskId);
            
            // Add to completed elements
            if (!state.CompletedElements.Contains(taskId))
            {
                state.CompletedElements.Add(taskId);
            }

            // Save updated state
            await _stateStore.SaveStateAsync(processInstanceId, state, version + 1);
                
            // ارسال رویداد کامل شدن وظیفه کاربری
            await _eventBus.PublishAsync(new Events.UserTaskCompletedEvent
            {
                ProcessInstanceId = processInstanceId,
                UserTaskId = taskId,
                UserId = userId ?? taskInfo.Assignee ?? "system",
                FormData = finalFormData
            }, cancellationToken);
            
            // انتشار رویداد تکمیل وظیفه
            await _eventBus.PublishAsync(new ElementCompleted
            {
                ProcessInstanceId = processInstanceId,
                ElementId = taskId,
                ElementType = "bpmn:UserTask"
            }, cancellationToken);
            
            _logger.LogInformation("Completed user task {TaskId} in process instance {ProcessInstanceId}", 
                taskId, processInstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing user task {TaskId} in process {ProcessId}", taskId, processInstanceId);
            throw new BpmnProcessorException($"Failed to complete task: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// دریافت اطلاعات جزئیات یک وظیفه کاربری
    /// </summary>
    public async Task<TaskInfo> GetUserTaskDetailsAsync(
        string processInstanceId,
        string taskId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(processInstanceId))
            throw new ArgumentException("Process instance ID cannot be empty", nameof(processInstanceId));
            
        if (string.IsNullOrEmpty(taskId))
            throw new ArgumentException("Task ID cannot be empty", nameof(taskId));
            
        var state = await GetProcessInstanceStateAsync(processInstanceId, cancellationToken);
        
        if (state.Tasks == null || !state.Tasks.TryGetValue(taskId, out var taskInfo))
            throw new BpmnProcessorException($"Task {taskId} not found in process instance {processInstanceId}");
            
        return taskInfo;
    }
    
    /// <summary>
    /// خاتمه دادن به نمونه فرآیند
    /// </summary>
    public async Task TerminateProcessInstanceAsync(
        string processInstanceId,
        string reason = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(processInstanceId))
            throw new ArgumentException("Process instance ID cannot be empty", nameof(processInstanceId));
            
        var (state, version) = await _stateStore.GetStateWithVersionAsync<BpmnProcessState>(processInstanceId, cancellationToken);
        if (state == null)
            throw new BpmnProcessorException($"Process instance {processInstanceId} not found");
        
        if (state.Status == ProcessStatus.Completed || state.Status == ProcessStatus.Deleted)
            throw new BpmnProcessorException($"Process instance {processInstanceId} is already terminated");
            
        try
        {
            // Update state
            state.Status = ProcessStatus.Deleted;
            state.ActiveElements.Clear();
            
            // Save updated state
            await _stateStore.SaveStateAsync(processInstanceId, state, version + 1);
            
            // انتشار رویداد حذف نمونه فرآیند
            await _eventBus.PublishAsync(new ProcessInstanceDeleted
            {
                ProcessInstanceId = processInstanceId,
                Reason = reason ?? "Manual termination"
            }, cancellationToken);
            
            _logger.LogInformation("Terminated process instance {ProcessInstanceId} with reason: {Reason}", 
                processInstanceId, reason ?? "Not specified");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error terminating process instance {ProcessId}", processInstanceId);
            throw new BpmnProcessorException($"Failed to terminate process instance: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// یافتن فرآیند با شناسه مشخص در تعریف BPMN
    /// </summary>
    private BpmnProcess FindProcess(BpmnDefinitions definitions, string processId = null)
    {
        if (definitions.Items == null || !definitions.Items.Any())
            return null;
            
        var processes = definitions.Items
            .OfType<BpmnProcess>()
            .ToList();
            
        if (!processes.Any())
            return null;
            
        if (string.IsNullOrEmpty(processId))
            return processes.First(); // اگر شناسه مشخص نشده، اولین فرآیند را برمی‌گرداند
            
        return processes.FirstOrDefault(p => p.id == processId);
    }
    
    /// <summary>
    /// یافتن رویداد شروع در یک فرآیند
    /// </summary>
    private BpmnStartEvent FindStartEvent(BpmnProcess process)
    {
        if (process.Items == null || !process.Items.Any())
            return null;
            
        return process.Items
            .OfType<BpmnStartEvent>()
            .FirstOrDefault();
    }
    
    /// <summary>
    /// استخراج مسیرهای خروجی از یک المان
    /// </summary>
    private List<string> GetOutgoingFlows(object element)
    {
        // در پیاده‌سازی واقعی، این روش باید با استفاده از مدل BPMN، مسیرهای خروجی را استخراج کند
        // برای سادگی، در اینجا یک لیست خالی برمی‌گردانیم
        return new List<string>();
    }

    private List<BpmnStartEvent> FindStartEvents(BpmnProcess process)
    {
        if (process?.Items == null || !process.Items.Any())
            return new List<BpmnStartEvent>();
            
        return process.Items
            .OfType<BpmnStartEvent>()
            .ToList();
    }
}

/// <summary>
/// رویداد شکست فرآیند
/// </summary>
public record ProcessInstanceFailed : Events.BpmnEvent
{
    /// <summary>
    /// پیام خطا
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// جزئیات خطا
    /// </summary>
    public string? ErrorDetails { get; init; }
}

/// <summary>
/// مدل اطلاعات نصب فرآیند
/// </summary>
public class BpmnDeploymentInfo
{
    /// <summary>
    /// کلید نصب
    /// </summary>
    public string DeploymentKey { get; set; } = string.Empty;
    
    /// <summary>
    /// شناسه تعریف
    /// </summary>
    public string DefinitionId { get; set; } = string.Empty;
    
    /// <summary>
    /// برچسب
    /// </summary>
    public string Label { get; set; } = string.Empty;
    
    /// <summary>
    /// محتوای XML
    /// </summary>
    public string XmlContent { get; set; } = string.Empty;
    
    /// <summary>
    /// زمان نصب
    /// </summary>
    public DateTime DeploymentTime { get; set; }
}

/// <summary>
/// خطای پردازش BPMN
/// </summary>
public class BpmnProcessorException : Exception
{
    public BpmnProcessorException(string message) : base(message)
    {
    }
    
    public BpmnProcessorException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
} 