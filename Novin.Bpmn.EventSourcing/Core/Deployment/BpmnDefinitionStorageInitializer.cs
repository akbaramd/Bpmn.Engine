using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;

namespace Novin.Bpmn.EventSourcing.Core.Deployment
{
    /// <summary>
    /// سرویس میزبانی برای راه‌اندازی سیستم ذخیره‌سازی تعاریف BPMN
    /// این سرویس در هنگام راه‌اندازی برنامه، مخزن و ذخیره‌سازی تعاریف را مقداردهی اولیه می‌کند
    /// </summary>
    public class BpmnDefinitionStorageInitializer : BackgroundService
    {
        private readonly IBpmnDefinitionStore _definitionStore;
        private readonly IBpmnDefinitionStorage _definitionStorage;
        private readonly ILogger<BpmnDefinitionStorageInitializer> _logger;

        /// <summary>
        /// ایجاد نمونه جدید از سرویس راه‌انداز ذخیره‌سازی تعاریف BPMN
        /// </summary>
        public BpmnDefinitionStorageInitializer(
            IBpmnDefinitionStore definitionStore,
            IBpmnDefinitionStorage definitionStorage,
            ILogger<BpmnDefinitionStorageInitializer> logger)
        {
            _definitionStore = definitionStore ?? throw new ArgumentNullException(nameof(definitionStore));
            _definitionStorage = definitionStorage ?? throw new ArgumentNullException(nameof(definitionStorage));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// اجرای سرویس پس‌زمینه
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("Initializing BPMN definition storage system");

                // مقداردهی اولیه مخزن تعاریف
                await _definitionStore.InitializeAsync(stoppingToken);
                _logger.LogInformation("BPMN definition store initialized");

                // مقداردهی اولیه ذخیره‌سازی حافظه‌ای تعاریف
                await _definitionStorage.InitializeAsync(stoppingToken);
                _logger.LogInformation("BPMN definition memory storage initialized with {Count} definitions", 
                    _definitionStorage.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing BPMN definition storage system");
                throw;
            }
        }
    }
} 