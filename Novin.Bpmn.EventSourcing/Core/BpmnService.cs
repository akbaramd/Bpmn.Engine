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
using Novin.Bpmn.EventSourcing.Core.Models;
using BpmnTaskInfo = Novin.Bpmn.EventSourcing.Core.Models.TaskInfo;
using Nest;
using Elasticsearch.Net;
namespace Novin.Bpmn.EventSourcing.Core;

/// <summary>
/// سرویس پردازش و مدیریت فرآیندهای BPMN
/// این سرویس مسئول خواندن تعریف BPMN، مدیریت نمونه‌های فرآیند و اجرای فرآیند با استفاده از Event Sourcing است
/// </summary>
public class BpmnService
{
    private readonly IEventBus _eventBus;
    private readonly IStateStore _stateStore;
    private readonly IEventStore _eventStore;
    private readonly IDefinitionStore _definitionStore;
    private readonly IElasticClient _elasticClient;
    private readonly ILogger<BpmnService> _logger;
    private const string DefinitionIndexPrefix = "bpmn-definitions-";
    
    /// <summary>
    /// ایجاد نمونه جدید از سرویس پردازش BPMN
    /// </summary>
    public BpmnService(
        IEventBus eventBus,
        IStateStore stateStore,
        IEventStore eventStore,
        IDefinitionStore definitionStore,
        IElasticClient elasticClient,
        ILogger<BpmnService> logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        _definitionStore = definitionStore ?? throw new ArgumentNullException(nameof(definitionStore));
        _elasticClient = elasticClient ?? throw new ArgumentNullException(nameof(elasticClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        EnsureDefinitionIndexTemplateAsync().GetAwaiter().GetResult();
    }

    private async Task EnsureDefinitionIndexTemplateAsync()
    {
        try
        {
            var templateName = DefinitionIndexPrefix + "template";
            var templateExists = await _elasticClient.Indices.TemplateExistsAsync(templateName);
            if (!templateExists.Exists)
            {
                var response = await _elasticClient.Indices.PutTemplateAsync(templateName, t => t
                    .Mappings(m => m
                        .Map<BpmnDefinitionDocument>(tm => tm
                            .Properties(p => p
                                .Keyword(k => k.Name("deploymentKey"))
                                .Keyword(k => k.Name("definitionId"))
                                .Keyword(k => k.Name("processId"))
                                .Text(t => t.Name("xmlContent"))
                                .Date(d => d.Name("deploymentTime"))
                                .Keyword(k => k.Name("label"))
                                .Number(n => n.Name("version").Type(NumberType.Integer)))))
                    .Settings(s => s
                        .NumberOfShards(1)
                        .NumberOfReplicas(0)
                        .RefreshInterval("1s"))
                    .IndexPatterns(DefinitionIndexPrefix + "*"));

                if (!response.IsValid)
                {
                    throw new ElasticsearchException($"Failed to create index template: {response.DebugInformation}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure definition index template exists");
            throw;
        }
    }
    
    /// <summary>
    /// تبدیل محتوای XML به مدل تعریف BPMN
    /// </summary>
    private BpmnDefinitions ParseBpmnXml(string xmlContent)
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
                
            _logger.LogInformation("Successfully parsed BPMN definition");
            return definitions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing BPMN XML content");
            throw new BpmnProcessorException("Failed to parse BPMN XML", ex);
        }
    }
    
    /// <summary>
    /// نصب تعریف فرآیند BPMN با پشتیبانی از نسخه‌گذاری
    /// </summary>
    public async Task<BpmnDeploymentInfo> DeployProcessDefinitionAsync(
        string deploymentKey, 
        string xmlContent,
        string label = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(deploymentKey))
            throw new ArgumentException("Deployment key cannot be empty", nameof(deploymentKey));
            
        if (string.IsNullOrEmpty(xmlContent))
            throw new ArgumentException("XML content cannot be empty", nameof(xmlContent));
            
        try
        {
            // Parse BPMN XML
            var definitions = ParseBpmnXml(xmlContent);
            
            // Get latest version for this deployment key
            var latestVersion = await GetLatestDeploymentVersionAsync(deploymentKey, cancellationToken);
            var newVersion = latestVersion + 1;
            
            // Generate unique definition ID
            var definitionId = $"{deploymentKey}-v{newVersion}";
            
            // Create deployment info
            var deploymentInfo = new BpmnDeploymentInfo
            {
                DeploymentKey = deploymentKey,
                DefinitionId = definitionId,
                Version = newVersion,
                Label = label ?? deploymentKey,
                XmlContent = xmlContent,
                DeploymentTime = DateTime.UtcNow
            };
            
            // Save to definition store
            await _definitionStore.SaveDefinitionAsync(
                deploymentKey, 
                xmlContent, 
                definitions, 
                label, 
                cancellationToken);
            
            // Save to Elasticsearch
            var indexName = $"{DefinitionIndexPrefix}{DateTime.UtcNow:yyyy-MM}";
            var document = new BpmnDefinitionDocument
            {
                DeploymentKey = deploymentKey,
                DefinitionId = definitionId,
                Version = newVersion,
                ProcessId = definitions.Items?.OfType<BpmnProcess>().FirstOrDefault()?.id,
                XmlContent = xmlContent,
                DeploymentTime = deploymentInfo.DeploymentTime,
                Label = label ?? deploymentKey
            };
            
            var response = await _elasticClient.IndexAsync(document, i => i
                .Index(indexName)
                .Id(definitionId)
                .Refresh(Elasticsearch.Net.Refresh.True),
                cancellationToken);
                
            if (!response.IsValid)
            {
                throw new ElasticsearchException($"Failed to save definition to Elasticsearch: {response.DebugInformation}");
            }
            
            _logger.LogInformation("Deployed BPMN process definition with key {DeploymentKey} and version {Version}", 
                deploymentKey, newVersion);
                
            return deploymentInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deploying process definition with key {DeploymentKey}", deploymentKey);
            throw new BpmnProcessorException($"Failed to deploy process definition: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// دریافت آخرین نسخه نصب شده برای یک کلید نصب
    /// </summary>
    private async Task<int> GetLatestDeploymentVersionAsync(string deploymentKey, CancellationToken cancellationToken)
    {
        var searchResponse = await _elasticClient.SearchAsync<BpmnDefinitionDocument>(s => s
            .Index(DefinitionIndexPrefix + "*")
            .Query(q => q
                .Term(t => t
                    .Field("deploymentKey.keyword")
                    .Value(deploymentKey)))
            .Sort(sort => sort
                .Descending("version"))
            .Size(1),
            cancellationToken);
            
        if (!searchResponse.IsValid)
        {
            throw new ElasticsearchException($"Failed to search for definition: {searchResponse.DebugInformation}");
        }
        
        var latestDoc = searchResponse.Documents.FirstOrDefault();
        return latestDoc?.Version ?? 0;
    }
    
    /// <summary>
    /// شروع یک نمونه جدید از فرآیند با کلید نصب
    /// </summary>
    public async Task<string> StartProcessInstanceAsync(
        string deploymentKey,
        string processId = null,
        Dictionary<string, object> variables = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(deploymentKey))
            throw new ArgumentException("Deployment key cannot be empty", nameof(deploymentKey));
            
        try
        {
            // Get latest version of definition
            var searchResponse = await _elasticClient.SearchAsync<BpmnDefinitionDocument>(s => s
                .Index(DefinitionIndexPrefix + "*")
                .Sort(sort => sort
                    .Descending("deploymentTime"))
                .Size(1),
                cancellationToken);
                
            if (!searchResponse.IsValid)
            {
                throw new ElasticsearchException($"Failed to search for definition: {searchResponse.DebugInformation}");
            }
            
            var definitionDoc = searchResponse.Documents.Where(x=>x.DeploymentKey == deploymentKey).FirstOrDefault();
            if (definitionDoc == null)
            {
                throw new BpmnProcessorException($"Process definition with key '{deploymentKey}' not found. Please deploy the definition first.");
            }

            // Parse the XML content to get the BPMN definition
            BpmnDefinitions definitions;
            try
            {
                definitions = ParseBpmnXml(definitionDoc.XmlContent);
            }
            catch (Exception ex)
            {
                throw new BpmnProcessorException($"Failed to parse BPMN definition for key '{deploymentKey}': {ex.Message}", ex);
            }
            
            if (definitions == null || definitions.Items == null || !definitions.Items.Any())
            {
                throw new BpmnProcessorException($"Invalid BPMN definition for key '{deploymentKey}': No process elements found");
            }
            
            var process = FindProcess(definitions, processId);
            if (process == null)
            {
                var availableProcesses = string.Join(", ", definitions.Items.OfType<BpmnProcess>().Select(p => p.id));
                throw new BpmnProcessorException(
                    $"Process ID '{processId ?? "<default>"}' not found in definition '{deploymentKey}'. " +
                    $"Available process IDs: {availableProcesses}");
            }
            
            // Generate process instance ID
            var processInstanceId = Guid.NewGuid().ToString();
            
            // Prepare initial variables
            Dictionary<string, object> initialVariables = variables != null 
                ? new Dictionary<string, object>(variables) 
                : new Dictionary<string, object>();
            
            // Create initial state
            var initialState = new BpmnProcessState
            {
                ProcessInstanceId = processInstanceId,
                ProcessDefinitionId = process.id,
                DeploymentKey = deploymentKey,
                DefinitionVersion = definitionDoc.Version,
                ActiveElements = new HashSet<string>(),
                CompletedElements = new HashSet<string>(),
                Variables = initialVariables,
                Status = ProcessStatus.Created,
                ExecutionPaths = new List<ExecutionPath>(),
                ActiveExecutions = new Dictionary<string, ExecutionPath>(),
                ElementExecutionPaths = new Dictionary<string, List<string>>(),
                ElementToSequenceFlows = new Dictionary<string, List<string>>(),
                ElementExecutionCounts = new Dictionary<string, int>(),
                GatewayMergeStates = new Dictionary<string, GatewayMergeInfo>(),
                EventToExecutionPath = new Dictionary<string, string>()
            };

            // Save initial state
            await _stateStore.SaveStateAsync(processInstanceId, initialState, null, cancellationToken);
            
            // Publish process instance created event
            var createdEvent = new ProcessInstanceCreated
            {
                ProcessInstanceId = processInstanceId,
                ProcessDefinitionId = process.id,
                ProcessDefinitionKey = deploymentKey,
                ProcessDefinitionVersion = definitionDoc.Version,
                Variables = initialVariables
            };
            await _eventBus.PublishAsync(createdEvent, cancellationToken);
            
            _logger.LogInformation("Started process instance {ProcessInstanceId} for definition {DeploymentKey} version {Version}", 
                processInstanceId, deploymentKey, definitionDoc.Version);
                
            return processInstanceId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting process instance for deployment {DeploymentKey}", deploymentKey);
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
            
        var state = await _stateStore.GetStateAsync(
            processInstanceId, cancellationToken);
            
        if (state == null)
            throw new BpmnProcessorException($"Process instance {processInstanceId} not found");
            
        return state;
    }
    
    /// <summary>
    /// دریافت تمام وظایف کاربری فعال برای یک نمونه فرآیند
    /// </summary>
    public async Task<Dictionary<string, BpmnTaskInfo>> GetUserTasksForProcessInstanceAsync(
        string processInstanceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(processInstanceId))
            throw new ArgumentException("Process instance ID cannot be empty", nameof(processInstanceId));
            
        var state = await GetProcessInstanceStateAsync(processInstanceId, cancellationToken);
        
        if (state.Tasks == null || !state.Tasks.Any())
            return new Dictionary<string, BpmnTaskInfo>();
            
        return state.Tasks
            .Where(t => t.Value.TaskType == "UserTask" && t.Value.Status.ToString() == "Active")
            .ToDictionary(t => t.Key, t => t.Value);
    }
    
    /// <summary>
    /// دریافت وظایف کاربری یک کاربر
    /// </summary>
    public async Task<Dictionary<string, Dictionary<string, BpmnTaskInfo>>> GetUserTasksForUserAsync(
        string userId,
        ICollection<string> userGroups = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
            
        // پیدا کردن فرآیندهای فعال
        var activeProcesses = await _stateStore.FindStatesByPatternAsync(
            "*", // همه فرآیندهای ذخیره شده
            state => state.Status == ProcessStatus.Running, 
            cancellationToken);
            
        var result = new Dictionary<string, Dictionary<string, BpmnTaskInfo>>();
        
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
    public async Task<BpmnTaskInfo> ClaimUserTaskAsync(
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
    /// دریافت اطلاعات جزئیات یک وظیفه کاربری
    /// </summary>
    public async Task<BpmnTaskInfo> GetUserTaskDetailsAsync(
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
            
        var (state, version) = await _stateStore.GetStateWithVersionAsync(processInstanceId, cancellationToken);
        if (state == null)
            throw new BpmnProcessorException($"Process instance {processInstanceId} not found");
        
        if (state.Status == ProcessStatus.Completed || state.Status == ProcessStatus.Terminated)
            throw new BpmnProcessorException($"Process instance {processInstanceId} is already terminated");
            
        try
        {
            // Update state
            state.Status = ProcessStatus.Terminated;
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

    /// <summary>
    /// ادامه اجرای یک نمونه فرآیند
    /// </summary>
    public async Task ContinueProcessInstanceAsync(
        string processInstanceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(processInstanceId))
            throw new ArgumentException("Process instance ID cannot be empty", nameof(processInstanceId));
            
        var state = await GetProcessInstanceStateAsync(processInstanceId, cancellationToken);
        
        if (state.Status == ProcessStatus.Completed || state.Status == ProcessStatus.Terminated)
            throw new BpmnProcessorException($"Process instance {processInstanceId} is already completed or terminated");
            
        try
        {
            // Publish continue event
            await _eventBus.PublishAsync(new ProcessInstanceContinued
            {
                ProcessInstanceId = processInstanceId,
                ProcessDefinitionId = state.ProcessDefinitionId,
                ProcessDefinitionKey = state.DeploymentKey,
                DefinitionVersion = state.DefinitionVersion
            }, cancellationToken);
            
            _logger.LogInformation("Continued process instance {ProcessInstanceId}", processInstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error continuing process instance {ProcessId}", processInstanceId);
            throw new BpmnProcessorException($"Failed to continue process instance: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// لغو یک نمونه فرآیند
    /// </summary>
    public async Task CancelProcessInstanceAsync(
        string processInstanceId,
        string reason = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(processInstanceId))
            throw new ArgumentException("Process instance ID cannot be empty", nameof(processInstanceId));
            
        var (state, version) = await _stateStore.GetStateWithVersionAsync(processInstanceId, cancellationToken);
        if (state == null)
            throw new BpmnProcessorException($"Process instance {processInstanceId} not found");
        
        if (state.Status == ProcessStatus.Completed || state.Status == ProcessStatus.Terminated)
            throw new BpmnProcessorException($"Process instance {processInstanceId} is already completed or terminated");
            
        try
        {
            // Update state
            state.Status = ProcessStatus.Cancelled;
            state.ActiveElements.Clear();
            
            // Save updated state
            await _stateStore.SaveStateAsync(processInstanceId, state, version + 1);
            
            // Publish cancellation event
            await _eventBus.PublishAsync(new ProcessInstanceCancelled
            {
                ProcessInstanceId = processInstanceId,
                ProcessDefinitionId = state.ProcessDefinitionId,
                ProcessDefinitionKey = state.DeploymentKey,
                DefinitionVersion = state.DefinitionVersion,
                Reason = reason ?? "Manual cancellation"
            }, cancellationToken);
            
            _logger.LogInformation("Cancelled process instance {ProcessInstanceId} with reason: {Reason}", 
                processInstanceId, reason ?? "Not specified");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling process instance {ProcessId}", processInstanceId);
            throw new BpmnProcessorException($"Failed to cancel process instance: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// شروع مجدد یک نمونه فرآیند از نقطه مشخص
    /// </summary>
    public async Task RestartProcessInstanceAsync(
        string processInstanceId,
        string startElementId = null,
        Dictionary<string, object> variables = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(processInstanceId))
            throw new ArgumentException("Process instance ID cannot be empty", nameof(processInstanceId));
            
        var state = await GetProcessInstanceStateAsync(processInstanceId, cancellationToken);
        
        if (state.Status != ProcessStatus.Cancelled && state.Status != ProcessStatus.Terminated)
            throw new BpmnProcessorException($"Process instance {processInstanceId} must be cancelled or terminated to restart");
            
        try
        {
            // Get definition
            var searchResponse = await _elasticClient.SearchAsync<BpmnDefinitionDocument>(s => s
                .Index(DefinitionIndexPrefix + "*")
                .Query(q => q
                    .Bool(b => b
                        .Must(
                            new QueryContainerDescriptor<BpmnDefinitionDocument>().Term(t => t.Field("deploymentKey.keyword").Value(state.DeploymentKey)),
                            new QueryContainerDescriptor<BpmnDefinitionDocument>().Term(t => t.Field("version").Value(state.DefinitionVersion)))))
                .Size(1),
                cancellationToken);
                
            if (!searchResponse.IsValid)
            {
                throw new ElasticsearchException($"Failed to search for definition: {searchResponse.DebugInformation}");
            }
            
            var definitionDoc = searchResponse.Documents.FirstOrDefault();
            if (definitionDoc == null)
            {
                throw new BpmnProcessorException($"Process definition not found for key {state.DeploymentKey} version {state.DefinitionVersion}");
            }
            
            // Update state
            state.Status = ProcessStatus.Created;
            state.ActiveElements.Clear();
            state.CompletedElements.Clear();
            
            if (variables != null)
            {
                state.Variables = new Dictionary<string, object>(variables);
            }
            
            // Save updated state
            await _stateStore.SaveStateAsync(processInstanceId, state, 0);
            
            // Publish restart event
            await _eventBus.PublishAsync(new ProcessInstanceRestarted
            {
                ProcessInstanceId = processInstanceId,
                ProcessDefinitionId = state.ProcessDefinitionId,
                ProcessDefinitionKey = state.DeploymentKey,
                DefinitionVersion = state.DefinitionVersion,
                StartElementId = startElementId
            }, cancellationToken);
            
            _logger.LogInformation("Restarted process instance {ProcessInstanceId} from element {StartElementId}", 
                processInstanceId, startElementId ?? "beginning");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restarting process instance {ProcessId}", processInstanceId);
            throw new BpmnProcessorException($"Failed to restart process instance: {ex.Message}", ex);
        }
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

/// <summary>
/// مدل سند تعریف BPMN در Elasticsearch
/// </summary>
public class BpmnDefinitionDocument
{
    [Keyword]
    public string DeploymentKey { get; set; } = string.Empty;
    
    [Keyword]
    public string DefinitionId { get; set; } = string.Empty;
    
    [Keyword]
    public string? ProcessId { get; set; }
    
    [Text]
    public string XmlContent { get; set; } = string.Empty;
    
    [Date]
    public DateTime DeploymentTime { get; set; }
    
    [Keyword]
    public string Label { get; set; } = string.Empty;
    
    [Number(NumberType.Integer)]
    public int Version { get; set; }
}

/// <summary>
/// رویداد ادامه اجرای فرآیند
/// </summary>
