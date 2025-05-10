using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;

namespace Novin.Bpmn.EventSourcing.Core.Deployment
{
    /// <summary>
    /// راه‌انداز مخزن تعاریف BPMN در زمان شروع برنامه
    /// </summary>
    public class BpmnDefinitionStoreInitializer : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BpmnDefinitionStoreInitializer> _logger;

        /// <summary>
        /// ایجاد نمونه جدید از راه‌انداز مخزن تعاریف BPMN
        /// </summary>
        public BpmnDefinitionStoreInitializer(
            IServiceProvider serviceProvider,
            ILogger<BpmnDefinitionStoreInitializer> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Initializing BPMN definition store...");
            
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var definitionStore = scope.ServiceProvider.GetRequiredService<IBpmnDefinitionStore>();
                
                // مقداردهی اولیه مخزن تعاریف
                await definitionStore.InitializeAsync(stoppingToken);
                
                _logger.LogInformation("BPMN definition store initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing BPMN definition store");
                throw;
            }
        }
    }
} 