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
    private readonly IProcessInstanceStateStore _stateStore;
    private readonly IEventStore _eventStore;
    private readonly IProcessDeploymentStore _deploymentStore;
    private readonly IElasticClient _elasticClient;
    private readonly ILogger<BpmnService> _logger;
    private const string DefinitionIndexPrefix = "bpmn-definitions-";
    
    /// <summary>
    /// ایجاد نمونه جدید از سرویس پردازش BPMN
    /// </summary>
    public BpmnService(
        IEventBus eventBus,
        IProcessInstanceStateStore stateStore,
        IEventStore eventStore,
        IProcessDeploymentStore deploymentStore,
        IElasticClient elasticClient,
        ILogger<BpmnService> logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        _deploymentStore = deploymentStore ?? throw new ArgumentNullException(nameof(deploymentStore));
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
                    throw new Exception($"Failed to create index template: {response.DebugInformation}");
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
    public async Task<ProcessDeploymentState> DeployProcessDefinitionAsync(
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


            var deploymentId = Guid.NewGuid();
            // Save to definition store
           var res =  await _deploymentStore.DeployAsync(
                deploymentKey, 
                deploymentId,
                xmlContent, 
                definitions, 
                label, 
                null,
                cancellationToken);

            //handlee rrros     
            if(res == null)
            {
                throw new BpmnProcessorException("Failed to deploy process definition");
            }
            
    
            _logger.LogInformation("Deployed BPMN process definition with key {DeploymentKey} and version {Version}", 
                deploymentKey, newVersion);
                
            return res;
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
            throw new Exception($"Failed to search for definition: {searchResponse.DebugInformation}");
        }
        
        var latestDoc = searchResponse.Documents.FirstOrDefault();
        return latestDoc?.Version ?? 0;
    }
    
    /// <summary>
    /// شروع یک نمونه جدید از فرآیند با کلید نصب
    /// </summary>
    public async Task<string> StartProcessInstanceAsync(
        Guid deploymentId,
        string processId = null,
        Dictionary<string, object> variables = null,
        CancellationToken cancellationToken = default)
    {

        try
        {
            
            
            var definitionDoc = await _deploymentStore.GetDeploymentAsync(deploymentId,cancellationToken);
            if (definitionDoc == null)
            {
                throw new BpmnProcessorException($"Process definition with key '{deploymentId}' not found. Please deploy the definition first.");
            }

            // Parse the XML content to get the BPMN definition
            BpmnDefinitions definitions;
            try
            {
                definitions = ParseBpmnXml(definitionDoc.XmlContent);
            }
            catch (Exception ex)
            {
                throw new BpmnProcessorException($"Failed to parse BPMN definition for key '{deploymentId}': {ex.Message}", ex);
            }
            
            if (definitions == null || definitions.Items == null || !definitions.Items.Any())
            {
                throw new BpmnProcessorException($"Invalid BPMN definition for key '{deploymentId}': No process elements found");
            }
            
            var process = FindProcess(definitions, processId);
            if (process == null)
            {
                var availableProcesses = string.Join(", ", definitions.Items.OfType<BpmnProcess>().Select(p => p.id));
                throw new BpmnProcessorException(
                    $"Process ID '{processId ?? "<default>"}' not found in definition '{deploymentId}'. " +
                    $"Available process IDs: {availableProcesses}");
            }
            
            // Generate process instance ID
            var processInstanceId = Guid.NewGuid().ToString();
            
            // Prepare initial variables
            Dictionary<string, object> initialVariables = variables != null 
                ? new Dictionary<string, object>(variables) 
                : new Dictionary<string, object>();
                
            // Create a new process instance state
            var initialState = ProcessInstanceState.From(new ProcessStarted 
            {
                InstanceId = processInstanceId,
                ProcessId = process.id,
                DeploymentId = definitionDoc.DeploymentId,
                DeploymentKey = definitionDoc.DeploymentKey,
                Timestamp = DateTime.UtcNow
            });
            
            
            // Add initial variables
            foreach (var variable in initialVariables)
            {
                initialState.SetVariable(variable.Key, variable.Value);
            }

            // Save initial state
            await _stateStore.UpsertAsync(initialState, null, cancellationToken);
            
            // Publish process started event
            var startedEvent = new ProcessStarted
            {
                InstanceId = processInstanceId,
                ProcessId = process.id,
                DeploymentId = definitionDoc.DeploymentId,
                DeploymentKey = definitionDoc.DeploymentKey,
                Timestamp = DateTime.UtcNow
            };
            await _eventBus.PublishAsync(startedEvent, cancellationToken);
            
            _logger.LogInformation("Started process instance {ProcessInstanceId} for definition {DeploymentKey} version {Version}", 
                processInstanceId, deploymentId, definitionDoc.Version);
                
            return processInstanceId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting process instance for deployment {DeploymentKey}", deploymentId);
            throw new BpmnProcessorException($"Failed to start process instance: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// دریافت وضعیت نمونه فرآیند
    /// </summary>
    public async Task<StateWithVersion<ProcessInstanceState>> GetProcessInstanceStateAsync(
        string processInstanceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(processInstanceId))
            throw new ArgumentException("Process instance ID cannot be empty", nameof(processInstanceId));
            
        var state = await _stateStore.GetAsync(
            processInstanceId, cancellationToken);
            
        if (state == null)
            throw new BpmnProcessorException($"Process instance {processInstanceId} not found");
            
        return state.Value;
    }


    /// <summary>
    /// خاتمه دادن به نمونه فرآیند
    /// </summary>
    public async Task TerminateProcessInstanceAsync(
        string processInstanceId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(processInstanceId))
            throw new ArgumentException("Process instance ID cannot be empty", nameof(processInstanceId));
            
        var stateInfo = await _stateStore.GetAsync(processInstanceId, cancellationToken);
        if (stateInfo == null)
            throw new BpmnProcessorException($"Process instance {processInstanceId} not found");
        
        var state = stateInfo.Value.State;
        var version = stateInfo.Value.Version;
        
        if (state.Status == ProcessInstanceStatus.Completed || state.Status == ProcessInstanceStatus.Terminated)
            throw new BpmnProcessorException($"Process instance {processInstanceId} is already terminated");
            
        try
        {
            
            var terminatedEvent = new ProcessTerminated
            {
                InstanceId = processInstanceId,
                DeploymentId = state.DeploymentId,
                DeploymentKey = state.DeploymentKey,
                ProcessId = state.ProcessId,
                TerminationReason = reason ?? "Manual termination"
            };

            // Update status to terminated
            state.Terminate(terminatedEvent);
            // Save updated state
            await _stateStore.UpsertAsync(state, ct: cancellationToken);
            
            // Publish termination event
            await _eventBus.PublishAsync(terminatedEvent, cancellationToken);
            
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
        
        if (state.State.Status == ProcessInstanceStatus.Completed || state.State.Status == ProcessInstanceStatus.Terminated)
            throw new BpmnProcessorException($"Process instance {processInstanceId} is already completed or terminated");
            
        try
        {
            // Publish continue event
            await _eventBus.PublishAsync(new ProcessResumed
            {
                InstanceId = processInstanceId,
                DeploymentId = state.State.DeploymentId,
                DeploymentKey = state.State.DeploymentKey,
                ProcessId = state.State.ProcessId,
                ResumeReason = "Manual resume"
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
            
        var stateInfo = await _stateStore.GetAsync(processInstanceId, cancellationToken);
        if (stateInfo == null)
            throw new BpmnProcessorException($"Process instance {processInstanceId} not found");
        
        var state = stateInfo.Value.State;
        var version = stateInfo.Value.Version;
        
        if (state.Status == ProcessInstanceStatus.Completed || state.Status == ProcessInstanceStatus.Terminated)
            throw new BpmnProcessorException($"Process instance {processInstanceId} is already completed or terminated");
            
        try
        {
            var cancelledEvent = new ProcessCancelled
            {
                InstanceId = processInstanceId,
                ProcessId = state.ProcessId,
                DeploymentKey = state.DeploymentKey,
                DeploymentId = state.DeploymentId,
                Reason = reason ?? "Manual cancellation",

            };
            // Update status
            state.Cancel(cancelledEvent);
            
            // Save updated state
            await _stateStore.UpsertAsync(state, ct: cancellationToken);
            
            // Publish cancellation event
            await _eventBus.PublishAsync(cancelledEvent, cancellationToken);
            
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
        
        if (state.State.Status != ProcessInstanceStatus.Cancelled && state.State.Status != ProcessInstanceStatus.Terminated)
            throw new BpmnProcessorException($"Process instance {processInstanceId} must be cancelled or terminated to restart");
            
        try
        {
            // Get definition
            var searchResponse = await _elasticClient.SearchAsync<BpmnDefinitionDocument>(s => s
                .Index(DefinitionIndexPrefix + "*")
                .Query(q => q
                    .Bool(b => b
                        .Must(
                            new QueryContainerDescriptor<BpmnDefinitionDocument>().Term(t => t.Field("deploymentKey.keyword").Value(state.State.DeploymentKey)),
                            new QueryContainerDescriptor<BpmnDefinitionDocument>().Term(t => t.Field("version").Value(state.Version)))))
                .Size(1),
                cancellationToken);
                
            if (!searchResponse.IsValid)
            {
                throw new Exception($"Failed to search for definition: {searchResponse.DebugInformation}");
            }
            
            var definitionDoc = searchResponse.Documents.FirstOrDefault();
            if (definitionDoc == null)
            {
                throw new BpmnProcessorException($"Process definition not found for key {state.State.DeploymentKey} version {state.Version}");
            }
            
            // Update state
            state.State.Restart();
            
            // Update variables if provided
            if (variables != null)
            {
                foreach (var variable in variables)
                {
                    state.State.SetVariable(variable.Key, variable.Value);
                }
            }
            
            // Save updated state
            await _stateStore.UpsertAsync(state.State, ct: cancellationToken);
            
            // Publish restart event
            await _eventBus.PublishAsync(new ProcessRestarted
            {
                InstanceId = processInstanceId,
                DeploymentId = state.State.DeploymentId,  
                DeploymentKey = state.State.DeploymentKey,
                ProcessId = state.State.ProcessId,
            }, cancellationToken);
            
            _logger.LogInformation("Restarted process instance {ProcessInstanceId}", 
                processInstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restarting process instance {ProcessId}", processInstanceId);
            throw new BpmnProcessorException($"Failed to restart process instance: {ex.Message}", ex);
        }
    }
}

[Serializable]
internal class BpmnProcessorException : Exception
{
    public BpmnProcessorException()
    {
    }

    public BpmnProcessorException(string? message) : base(message)
    {
    }

    public BpmnProcessorException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}