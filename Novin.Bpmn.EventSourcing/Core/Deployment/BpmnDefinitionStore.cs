using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.Models;

namespace Novin.Bpmn.EventSourcing.Core.Deployment
{
    /// <summary>
    /// مخزن تعاریف BPMN با قابلیت ذخیره‌سازی دائمی و کش در حافظه
    /// </summary>
    public class BpmnDefinitionStore : IBpmnDefinitionStore
    {
        private readonly IStateStore _stateStore;
        private readonly ILogger<BpmnDefinitionStore> _logger;
        private readonly ConcurrentDictionary<string, BpmnDeploymentInfo> _deploymentCache;
        private readonly ConcurrentDictionary<string, BpmnDefinitions> _definitionsCache;
        private readonly string _definitionsDirectory;
        private bool _isInitialized = false;
        private readonly SemaphoreSlim _initializationLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// ایجاد نمونه جدید از مخزن تعاریف BPMN
        /// </summary>
        public BpmnDefinitionStore(
            IStateStore stateStore,
            ILogger<BpmnDefinitionStore> logger,
            string definitionsDirectory = null)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _deploymentCache = new ConcurrentDictionary<string, BpmnDeploymentInfo>();
            _definitionsCache = new ConcurrentDictionary<string, BpmnDefinitions>();
            _definitionsDirectory = definitionsDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Definitions");
        }

        /// <summary>
        /// مقداردهی اولیه و بارگذاری تعاریف از حافظه دائمی
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            // از سمافور برای اطمینان از اینکه فقط یک بار مقداردهی اولیه انجام می‌شود استفاده می‌کنیم
            await _initializationLock.WaitAsync(cancellationToken);
            try
            {
                if (_isInitialized)
                    return;

                _logger.LogInformation("Initializing BPMN definition store from persistent storage");

                // اطمینان از وجود دایرکتوری ذخیره‌سازی تعاریف
                EnsureDefinitionsDirectoryExists();

                // بازیابی تمام اطلاعات نصب از StateStore
                var deployments = await _stateStore.FindStatesByPatternAsync<BpmnDeploymentInfo>(
                    "deployment:*", 
                    deployment => true,
                    cancellationToken);

                // بارگذاری اطلاعات نصب در کش
                foreach (var deployment in deployments)
                {
                    _deploymentCache[deployment.DeploymentKey] = deployment;
                    _logger.LogDebug("Loaded deployment info for key {DeploymentKey}", deployment.DeploymentKey);
                }

                _logger.LogInformation("Loaded {Count} BPMN process definitions into memory", _deploymentCache.Count);
                _isInitialized = true;
            }
            finally
            {
                _initializationLock.Release();
            }
        }

        /// <summary>
        /// ذخیره تعریف BPMN جدید
        /// </summary>
        public async Task<string> SaveDefinitionAsync(
            string deploymentKey,
            string xmlContent,
            BpmnDefinitions parsedDefinitions,
            string label = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(deploymentKey))
                throw new ArgumentException("Deployment key cannot be empty", nameof(deploymentKey));

            if (string.IsNullOrEmpty(xmlContent))
                throw new ArgumentException("XML content cannot be empty", nameof(xmlContent));

            if (parsedDefinitions == null)
                throw new ArgumentNullException(nameof(parsedDefinitions));

            // اطمینان از مقداردهی اولیه
            if (!_isInitialized)
                await InitializeAsync(cancellationToken);

            // ایجاد اطلاعات نصب جدید
            var definitionId = parsedDefinitions.id ?? Guid.NewGuid().ToString();
            var deploymentInfo = new BpmnDeploymentInfo
            {
                DeploymentKey = deploymentKey,
                DefinitionId = definitionId,
                Label = label ?? deploymentKey,
                XmlContent = xmlContent,
                DeploymentTime = DateTime.UtcNow
            };

            // ذخیره در مخزن حالت
            await _stateStore.SaveStateAsync($"deployment:{deploymentKey}", deploymentInfo, null, cancellationToken);
            
            // ذخیره در فایل
            await SaveDefinitionToFileAsync(deploymentKey, xmlContent, cancellationToken);

            // افزودن به کش
            _deploymentCache[deploymentKey] = deploymentInfo;
            _definitionsCache[deploymentKey] = parsedDefinitions;

            _logger.LogInformation("Saved BPMN process definition with key {DeploymentKey} and ID {DefinitionId}",
                deploymentKey, definitionId);

            return definitionId;
        }

        /// <summary>
        /// بازیابی اطلاعات تعریف BPMN بر اساس کلید نصب
        /// </summary>
        public async Task<BpmnDeploymentInfo> GetDeploymentInfoAsync(
            string deploymentKey,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(deploymentKey))
                throw new ArgumentException("Deployment key cannot be empty", nameof(deploymentKey));

            // اطمینان از مقداردهی اولیه
            if (!_isInitialized)
                await InitializeAsync(cancellationToken);

            // تلاش برای بازیابی از کش
            if (_deploymentCache.TryGetValue(deploymentKey, out var cachedInfo))
                return cachedInfo;

            // بازیابی از StateStore
            var deploymentInfo = await _stateStore.GetStateAsync<BpmnDeploymentInfo>(
                $"deployment:{deploymentKey}", cancellationToken);

            if (deploymentInfo == null)
            {
                _logger.LogWarning("Deployment with key {DeploymentKey} not found", deploymentKey);
                return null;
            }

            // افزودن به کش
            _deploymentCache[deploymentKey] = deploymentInfo;
            return deploymentInfo;
        }

        /// <summary>
        /// بازیابی تعریف پارس‌شده BPMN بر اساس کلید نصب
        /// </summary>
        public async Task<BpmnDefinitions> GetParsedDefinitionAsync(
            string deploymentKey,
            Func<string, BpmnDefinitions> xmlParser,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(deploymentKey))
                throw new ArgumentException("Deployment key cannot be empty", nameof(deploymentKey));

            if (xmlParser == null)
                throw new ArgumentNullException(nameof(xmlParser));

            // اطمینان از مقداردهی اولیه
            if (!_isInitialized)
                await InitializeAsync(cancellationToken);

            // تلاش برای بازیابی از کش
            if (_definitionsCache.TryGetValue(deploymentKey, out var cachedDefinitions))
                return cachedDefinitions;

            // بازیابی اطلاعات نصب
            var deploymentInfo = await GetDeploymentInfoAsync(deploymentKey, cancellationToken);
            if (deploymentInfo == null)
                return null;

            try
            {
                // پارس کردن محتوای XML
                var definitions = xmlParser(deploymentInfo.XmlContent);
                
                // افزودن به کش
                _definitionsCache[deploymentKey] = definitions;
                
                return definitions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing BPMN XML for deployment {DeploymentKey}", deploymentKey);
                throw new BpmnProcessorException($"Failed to parse BPMN XML for deployment {deploymentKey}", ex);
            }
        }

        /// <summary>
        /// دریافت تمام کلیدهای نصب موجود
        /// </summary>
        public async Task<IList<string>> GetAllDeploymentKeysAsync(CancellationToken cancellationToken = default)
        {
            // اطمینان از مقداردهی اولیه
            if (!_isInitialized)
                await InitializeAsync(cancellationToken);

            return _deploymentCache.Keys.ToList();
        }

        /// <summary>
        /// حذف تعریف BPMN
        /// </summary>
        public async Task DeleteDefinitionAsync(string deploymentKey, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(deploymentKey))
                throw new ArgumentException("Deployment key cannot be empty", nameof(deploymentKey));

            // اطمینان از مقداردهی اولیه
            if (!_isInitialized)
                await InitializeAsync(cancellationToken);

            // حذف از StateStore
            await _stateStore.DeleteStateAsync($"deployment:{deploymentKey}", null, cancellationToken);
            
            // حذف فایل
            DeleteDefinitionFile(deploymentKey);

            // حذف از کش‌ها
            _deploymentCache.TryRemove(deploymentKey, out _);
            _definitionsCache.TryRemove(deploymentKey, out _);

            _logger.LogInformation("Deleted BPMN process definition with key {DeploymentKey}", deploymentKey);
        }

        /// <summary>
        /// ذخیره تعریف در فایل
        /// </summary>
        private async Task SaveDefinitionToFileAsync(string deploymentKey, string xmlContent, CancellationToken cancellationToken)
        {
            try
            {
                EnsureDefinitionsDirectoryExists();
                string filePath = GetDefinitionFilePath(deploymentKey);
                await File.WriteAllTextAsync(filePath, xmlContent, cancellationToken);
                _logger.LogDebug("Saved BPMN definition to file: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving BPMN definition to file for key {DeploymentKey}", deploymentKey);
                // ادامه می‌دهیم چون خطای ذخیره در فایل نباید کل عملیات را متوقف کند
            }
        }

        /// <summary>
        /// حذف فایل تعریف
        /// </summary>
        private void DeleteDefinitionFile(string deploymentKey)
        {
            try
            {
                string filePath = GetDefinitionFilePath(deploymentKey);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogDebug("Deleted BPMN definition file: {FilePath}", filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting BPMN definition file for key {DeploymentKey}", deploymentKey);
            }
        }

        /// <summary>
        /// اطمینان از وجود دایرکتوری ذخیره‌سازی
        /// </summary>
        private void EnsureDefinitionsDirectoryExists()
        {
            if (!Directory.Exists(_definitionsDirectory))
            {
                Directory.CreateDirectory(_definitionsDirectory);
                _logger.LogInformation("Created definitions directory: {Directory}", _definitionsDirectory);
            }
        }

        /// <summary>
        /// دریافت مسیر فایل تعریف
        /// </summary>
        private string GetDefinitionFilePath(string deploymentKey)
        {
            // حذف کاراکترهای غیرمجاز در نام فایل
            string safeFileName = string.Join("_", deploymentKey.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(_definitionsDirectory, $"{safeFileName}.bpmn");
        }
    }
} 