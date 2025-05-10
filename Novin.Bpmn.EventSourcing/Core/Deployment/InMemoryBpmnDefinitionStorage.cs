using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.Models;

namespace Novin.Bpmn.EventSourcing.Core.Deployment
{
    /// <summary>
    /// پیاده‌سازی ذخیره‌سازی حافظه‌ای تعاریف BPMN
    /// این کلاس تمام مدل‌ها را در حافظه نگه می‌دارد و نیازی به ذخیره‌سازی دائمی ندارد
    /// شاخص‌های چندگانه برای دسترسی سریع‌تر به داده‌ها نیز در این کلاس پیاده‌سازی شده است
    /// </summary>
    public class InMemoryBpmnDefinitionStorage : IBpmnDefinitionStorage
    {
        private readonly ILogger<InMemoryBpmnDefinitionStorage> _logger;
        private readonly ConcurrentDictionary<string, BpmnDeploymentInfo> _deploymentInfos;
        private readonly ConcurrentDictionary<string, BpmnDefinitions> _parsedDefinitions;
        
        // شاخص دسترسی سریع بر اساس شناسه فرآیند
        private readonly ConcurrentDictionary<string, HashSet<string>> _processIdToDeploymentKeys;
        
        // شاخص دسترسی سریع بر اساس کلید پیام‌های شروع‌کننده
        private readonly ConcurrentDictionary<string, HashSet<string>> _messageKeyToDeploymentKeys;
        
        // شاخص دسترسی سریع بر اساس نام رویداد
        private readonly ConcurrentDictionary<string, HashSet<string>> _eventNameToDeploymentKeys;
        
        // شاخص دسترسی سریع بر اساس نوع المان
        private readonly ConcurrentDictionary<string, HashSet<string>> _elementTypeToDeploymentKeys;
        
        // اطلاعات تحلیلی فرآیندها
        private readonly ConcurrentDictionary<string, ProcessMetadata> _processMetadata;
        
        private SemaphoreSlim _initializationLock = new SemaphoreSlim(1, 1);
        private bool _isInitialized = false;

        /// <summary>
        /// ایجاد نمونه جدید از ذخیره‌سازی حافظه‌ای تعاریف BPMN
        /// </summary>
        public InMemoryBpmnDefinitionStorage(ILogger<InMemoryBpmnDefinitionStorage> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _deploymentInfos = new ConcurrentDictionary<string, BpmnDeploymentInfo>();
            _parsedDefinitions = new ConcurrentDictionary<string, BpmnDefinitions>();
            _processIdToDeploymentKeys = new ConcurrentDictionary<string, HashSet<string>>();
            _messageKeyToDeploymentKeys = new ConcurrentDictionary<string, HashSet<string>>();
            _eventNameToDeploymentKeys = new ConcurrentDictionary<string, HashSet<string>>();
            _elementTypeToDeploymentKeys = new ConcurrentDictionary<string, HashSet<string>>();
            _processMetadata = new ConcurrentDictionary<string, ProcessMetadata>();
        }

        /// <summary>
        /// تعداد تعاریف موجود در حافظه
        /// </summary>
        public int Count => _deploymentInfos.Count;

        /// <summary>
        /// مقداردهی اولیه
        /// در این پیاده‌سازی، عملیات خاصی در زمان مقداردهی اولیه انجام نمی‌شود
        /// </summary>
        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            // اطمینان از اینکه فقط یک بار مقداردهی انجام می‌شود
            _initializationLock.WaitAsync(cancellationToken).GetAwaiter().GetResult();
            try
            {
                if (_isInitialized)
                    return Task.CompletedTask;

                _logger.LogInformation("Initializing in-memory BPMN definition storage");
                _isInitialized = true;
            }
            finally
            {
                _initializationLock.Release();
            }
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// افزودن تعریف BPMN جدید به حافظه
        /// </summary>
        public string AddDefinition(
            string deploymentKey,
            BpmnDeploymentInfo definitionInfo,
            BpmnDefinitions parsedDefinition)
        {
            if (string.IsNullOrEmpty(deploymentKey))
                throw new ArgumentException("Deployment key cannot be empty", nameof(deploymentKey));
            
            if (definitionInfo == null)
                throw new ArgumentNullException(nameof(definitionInfo));
            
            if (parsedDefinition == null)
                throw new ArgumentNullException(nameof(parsedDefinition));

            _deploymentInfos[deploymentKey] = definitionInfo;
            _parsedDefinitions[deploymentKey] = parsedDefinition;
            
            // بروزرسانی شاخص‌ها
            UpdateProcessIdIndex(deploymentKey, parsedDefinition);
            
            // تحلیل محتوای XML برای استخراج متادیتا
            ExtractAndIndexMetadata(deploymentKey, definitionInfo.XmlContent, parsedDefinition);
            
            _logger.LogDebug("Added definition {DeploymentKey} to memory storage with full metadata indexing", deploymentKey);
            
            return definitionInfo.DefinitionId;
        }

        /// <summary>
        /// دریافت اطلاعات تعریف با کلید مشخص
        /// </summary>
        public BpmnDeploymentInfo GetDeploymentInfo(string deploymentKey)
        {
            if (string.IsNullOrEmpty(deploymentKey))
                throw new ArgumentException("Deployment key cannot be empty", nameof(deploymentKey));
            
            if (_deploymentInfos.TryGetValue(deploymentKey, out var info))
                return info;
            
            return null;
        }

        /// <summary>
        /// دریافت تعریف پارس شده با کلید مشخص
        /// </summary>
        public BpmnDefinitions GetParsedDefinition(string deploymentKey)
        {
            if (string.IsNullOrEmpty(deploymentKey))
                throw new ArgumentException("Deployment key cannot be empty", nameof(deploymentKey));
            
            if (_parsedDefinitions.TryGetValue(deploymentKey, out var definition))
                return definition;
            
            return null;
        }

        /// <summary>
        /// دریافت تمام کلیدهای نصب موجود
        /// </summary>
        public IReadOnlyList<string> GetAllDeploymentKeys()
        {
            return _deploymentInfos.Keys.ToList();
        }

        /// <summary>
        /// جستجوی تعاریف بر اساس شرط
        /// </summary>
        public IReadOnlyList<BpmnDeploymentInfo> FindDeployments(Func<BpmnDeploymentInfo, bool> predicate)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));
            
            return _deploymentInfos.Values
                .Where(predicate)
                .ToList();
        }

        /// <summary>
        /// جستجوی تعاریف بر اساس شناسه فرآیند
        /// </summary>
        public IReadOnlyList<BpmnDeploymentInfo> FindDeploymentsByProcessId(string processId)
        {
            if (string.IsNullOrEmpty(processId))
                throw new ArgumentException("Process ID cannot be empty", nameof(processId));
            
            // استفاده از شاخص برای جستجوی سریع‌تر
            if (_processIdToDeploymentKeys.TryGetValue(processId, out var deploymentKeys))
            {
                return deploymentKeys
                    .Select(key => _deploymentInfos.TryGetValue(key, out var info) ? info : null)
                    .Where(info => info != null)
                    .ToList();
            }
            
            return new List<BpmnDeploymentInfo>();
        }

        /// <summary>
        /// جستجوی تعاریف بر اساس کلید پیام
        /// </summary>
        public IReadOnlyList<BpmnDeploymentInfo> FindDeploymentsByMessageKey(string messageKey)
        {
            if (string.IsNullOrEmpty(messageKey))
                throw new ArgumentException("Message key cannot be empty", nameof(messageKey));
            
            // استفاده از شاخص برای جستجوی سریع‌تر
            if (_messageKeyToDeploymentKeys.TryGetValue(messageKey, out var deploymentKeys))
            {
                return deploymentKeys
                    .Select(key => _deploymentInfos.TryGetValue(key, out var info) ? info : null)
                    .Where(info => info != null)
                    .ToList();
            }
            
            return new List<BpmnDeploymentInfo>();
        }

        /// <summary>
        /// جستجوی تعاریف بر اساس نام رویداد
        /// </summary>
        public IReadOnlyList<BpmnDeploymentInfo> FindDeploymentsByEventName(string eventName)
        {
            if (string.IsNullOrEmpty(eventName))
                throw new ArgumentException("Event name cannot be empty", nameof(eventName));
            
            // استفاده از شاخص برای جستجوی سریع‌تر
            if (_eventNameToDeploymentKeys.TryGetValue(eventName, out var deploymentKeys))
            {
                return deploymentKeys
                    .Select(key => _deploymentInfos.TryGetValue(key, out var info) ? info : null)
                    .Where(info => info != null)
                    .ToList();
            }
            
            return new List<BpmnDeploymentInfo>();
        }

        /// <summary>
        /// جستجوی تعاریف بر اساس نوع المان
        /// </summary>
        public IReadOnlyList<BpmnDeploymentInfo> FindDeploymentsByElementType(string elementType)
        {
            if (string.IsNullOrEmpty(elementType))
                throw new ArgumentException("Element type cannot be empty", nameof(elementType));
            
            // استفاده از شاخص برای جستجوی سریع‌تر
            if (_elementTypeToDeploymentKeys.TryGetValue(elementType, out var deploymentKeys))
            {
                return deploymentKeys
                    .Select(key => _deploymentInfos.TryGetValue(key, out var info) ? info : null)
                    .Where(info => info != null)
                    .ToList();
            }
            
            return new List<BpmnDeploymentInfo>();
        }

        /// <summary>
        /// دریافت متادیتای فرآیند
        /// </summary>
        public ProcessMetadata GetProcessMetadata(string deploymentKey)
        {
            if (string.IsNullOrEmpty(deploymentKey))
                throw new ArgumentException("Deployment key cannot be empty", nameof(deploymentKey));
            
            if (_processMetadata.TryGetValue(deploymentKey, out var metadata))
                return metadata;
            
            return null;
        }

        /// <summary>
        /// آیا تعریف با کلید مشخص وجود دارد
        /// </summary>
        public bool HasDefinition(string deploymentKey)
        {
            if (string.IsNullOrEmpty(deploymentKey))
                throw new ArgumentException("Deployment key cannot be empty", nameof(deploymentKey));
            
            return _deploymentInfos.ContainsKey(deploymentKey);
        }

        /// <summary>
        /// حذف تعریف از حافظه
        /// </summary>
        public bool RemoveDefinition(string deploymentKey)
        {
            if (string.IsNullOrEmpty(deploymentKey))
                throw new ArgumentException("Deployment key cannot be empty", nameof(deploymentKey));
            
            // حذف از کش‌های اصلی
            bool removed = _deploymentInfos.TryRemove(deploymentKey, out var removedInfo);
            _parsedDefinitions.TryRemove(deploymentKey, out _);
            _processMetadata.TryRemove(deploymentKey, out _);
            
            // حذف از شاخص‌ها
            if (removed)
            {
                // حذف از شاخص شناسه فرآیند
                foreach (var processIdEntry in _processIdToDeploymentKeys)
                {
                    processIdEntry.Value.Remove(deploymentKey);
                }
                
                // حذف از شاخص کلید پیام
                foreach (var messageKeyEntry in _messageKeyToDeploymentKeys)
                {
                    messageKeyEntry.Value.Remove(deploymentKey);
                }
                
                // حذف از شاخص نام رویداد
                foreach (var eventNameEntry in _eventNameToDeploymentKeys)
                {
                    eventNameEntry.Value.Remove(deploymentKey);
                }
                
                // حذف از شاخص نوع المان
                foreach (var elementTypeEntry in _elementTypeToDeploymentKeys)
                {
                    elementTypeEntry.Value.Remove(deploymentKey);
                }
                
                _logger.LogDebug("Removed definition {DeploymentKey} from memory storage", deploymentKey);
            }
            
            return removed;
        }

        /// <summary>
        /// به‌روزرسانی شاخص شناسه فرآیند
        /// </summary>
        private void UpdateProcessIdIndex(string deploymentKey, BpmnDefinitions definitions)
        {
            if (definitions?.Items == null)
                return;
            
            // یافتن همه فرآیندها در تعریف
            var processes = definitions.Items
                .OfType<BpmnProcess>()
                .Where(p => !string.IsNullOrEmpty(p.id))
                .ToList();
            
            foreach (var process in processes)
            {
                var processId = process.id;
                
                // افزودن به شاخص شناسه فرآیند
                _processIdToDeploymentKeys.AddOrUpdate(
                    processId,
                    // اگر کلید وجود ندارد، یک مجموعه جدید ایجاد می‌کنیم
                    _ => new HashSet<string> { deploymentKey },
                    // اگر کلید وجود دارد، به مجموعه موجود اضافه می‌کنیم
                    (_, set) => 
                    {
                        set.Add(deploymentKey);
                        return set;
                    });
            }
        }

        /// <summary>
        /// استخراج و ذخیره‌سازی متادیتای فرآیند
        /// </summary>
        private void ExtractAndIndexMetadata(string deploymentKey, string xmlContent, BpmnDefinitions definitions)
        {
            try
            {
                // ایجاد متادیتای جدید
                var metadata = new ProcessMetadata
                {
                    DeploymentKey = deploymentKey,
                    ProcessIds = new List<string>(),
                    MessageKeys = new List<string>(),
                    EventNames = new List<string>(),
                    ElementTypes = new List<string>(),
                    StartEventTypes = new List<string>(),
                    HasUserTasks = false,
                    HasServiceTasks = false,
                    HasTimers = false,
                    HasMessageEvents = false
                };
                
                // تحلیل XML برای یافتن اطلاعات بیشتر
                if (!string.IsNullOrEmpty(xmlContent))
                {
                    try
                    {
                        var doc = XDocument.Parse(xmlContent);
                        XNamespace ns = doc.Root.GetDefaultNamespace();
                        
                        // استخراج رویدادهای پیام و کلیدهای پیام
                        var messageEvents = doc.Descendants(ns + "messageEventDefinition").ToList();
                        var messageEventIds = messageEvents
                            .Select(e => e.Parent?.Attribute("id")?.Value)
                            .Where(id => !string.IsNullOrEmpty(id))
                            .ToList();
                        
                        metadata.HasMessageEvents = messageEvents.Any();
                        
                        // استخراج کلیدهای پیام از messageRef
                        var messageRefs = messageEvents
                            .Select(e => e.Attribute("messageRef")?.Value)
                            .Where(r => !string.IsNullOrEmpty(r))
                            .ToList();
                        
                        // استخراج تعاریف پیام و کلیدهای آنها
                        var messageDefinitions = doc.Descendants(ns + "message").ToList();
                        var messageKeys = messageDefinitions
                            .Select(m => m.Attribute("id")?.Value)
                            .Where(id => !string.IsNullOrEmpty(id))
                            .ToList();
                        
                        metadata.MessageKeys.AddRange(messageKeys);
                        metadata.MessageKeys.AddRange(messageRefs);
                        
                        // استخراج رویدادهای تایمر
                        var timerEvents = doc.Descendants(ns + "timerEventDefinition").ToList();
                        metadata.HasTimers = timerEvents.Any();
                        
                        // استخراج وظایف کاربری
                        var userTasks = doc.Descendants(ns + "userTask").ToList();
                        metadata.HasUserTasks = userTasks.Any();
                        
                        // استخراج وظایف سرویس
                        var serviceTasks = doc.Descendants(ns + "serviceTask").ToList();
                        metadata.HasServiceTasks = serviceTasks.Any();
                        
                        // استخراج انواع المان‌ها
                        var allElements = doc.Descendants()
                            .Where(e => e.Name.Namespace == ns)
                            .ToList();
                        
                        var elementTypes = allElements
                            .Select(e => e.Name.LocalName)
                            .Distinct()
                            .ToList();
                        
                        metadata.ElementTypes.AddRange(elementTypes);
                        
                        // افزودن به شاخص‌های عناصر
                        foreach (var elementType in elementTypes)
                        {
                            _elementTypeToDeploymentKeys.AddOrUpdate(
                                elementType,
                                _ => new HashSet<string> { deploymentKey },
                                (_, set) => { set.Add(deploymentKey); return set; });
                        }
                        
                        // استخراج نام‌های رویدادها
                        var events = allElements
                            .Where(e => e.Name.LocalName.Contains("Event"))
                            .ToList();
                        
                        var eventNames = events
                            .Select(e => e.Attribute("name")?.Value)
                            .Where(name => !string.IsNullOrEmpty(name))
                            .Distinct()
                            .ToList();
                        
                        metadata.EventNames.AddRange(eventNames);
                        
                        // افزودن به شاخص‌های نام رویداد
                        foreach (var eventName in eventNames)
                        {
                            _eventNameToDeploymentKeys.AddOrUpdate(
                                eventName,
                                _ => new HashSet<string> { deploymentKey },
                                (_, set) => { set.Add(deploymentKey); return set; });
                        }
                        
                        // استخراج انواع رویداد شروع
                        var startEvents = doc.Descendants(ns + "startEvent").ToList();
                        var startEventTypes = new List<string>();
                        
                        foreach (var startEvent in startEvents)
                        {
                            if (startEvent.Descendants(ns + "messageEventDefinition").Any())
                                startEventTypes.Add("message");
                            else if (startEvent.Descendants(ns + "timerEventDefinition").Any())
                                startEventTypes.Add("timer");
                            else if (startEvent.Descendants(ns + "signalEventDefinition").Any())
                                startEventTypes.Add("signal");
                            else
                                startEventTypes.Add("none");
                        }
                        
                        metadata.StartEventTypes.AddRange(startEventTypes.Distinct());
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error extracting additional metadata from XML for {DeploymentKey}", deploymentKey);
                    }
                }
                
                // استخراج شناسه‌های فرآیند از مدل پارس شده
                if (definitions?.Items != null)
                {
                    var processes = definitions.Items
                        .OfType<BpmnProcess>()
                        .Where(p => !string.IsNullOrEmpty(p.id))
                        .ToList();
                    
                    metadata.ProcessIds.AddRange(processes.Select(p => p.id));
                }
                
                // افزودن متادیتا به مخزن
                _processMetadata[deploymentKey] = metadata;
                
                // افزودن به شاخص‌های کلید پیام
                foreach (var messageKey in metadata.MessageKeys)
                {
                    _messageKeyToDeploymentKeys.AddOrUpdate(
                        messageKey,
                        _ => new HashSet<string> { deploymentKey },
                        (_, set) => { set.Add(deploymentKey); return set; });
                }
                
                _logger.LogDebug("Extracted and indexed metadata for {DeploymentKey}: " +
                                "{MessageCount} message keys, {EventCount} event names",
                    deploymentKey, metadata.MessageKeys.Count, metadata.EventNames.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting metadata for {DeploymentKey}", deploymentKey);
            }
        }
    }

    /// <summary>
    /// متادیتای فرآیند برای جستجو و تحلیل
    /// </summary>
    public class ProcessMetadata
    {
        /// <summary>
        /// کلید نصب
        /// </summary>
        public string DeploymentKey { get; set; }
        
        /// <summary>
        /// شناسه‌های فرآیند
        /// </summary>
        public List<string> ProcessIds { get; set; }
        
        /// <summary>
        /// کلیدهای پیام
        /// </summary>
        public List<string> MessageKeys { get; set; }
        
        /// <summary>
        /// نام‌های رویدادها
        /// </summary>
        public List<string> EventNames { get; set; }
        
        /// <summary>
        /// انواع المان‌ها
        /// </summary>
        public List<string> ElementTypes { get; set; }
        
        /// <summary>
        /// انواع رویداد شروع
        /// </summary>
        public List<string> StartEventTypes { get; set; }
        
        /// <summary>
        /// آیا فرآیند دارای وظایف کاربری است
        /// </summary>
        public bool HasUserTasks { get; set; }
        
        /// <summary>
        /// آیا فرآیند دارای وظایف سرویس است
        /// </summary>
        public bool HasServiceTasks { get; set; }
        
        /// <summary>
        /// آیا فرآیند دارای تایمر است
        /// </summary>
        public bool HasTimers { get; set; }
        
        /// <summary>
        /// آیا فرآیند دارای رویدادهای پیام است
        /// </summary>
        public bool HasMessageEvents { get; set; }
    }
} 