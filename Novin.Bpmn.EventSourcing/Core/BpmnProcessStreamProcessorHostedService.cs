using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Novin.Bpmn.EventSourcing.Core;

/// <summary>
/// سرویس میزبان برای اجرای پردازشگر جریان فرآیند BPMN در پس‌زمینه
/// </summary>
public class BpmnProcessStreamProcessorHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BpmnProcessStreamProcessorHostedService> _logger;

    /// <summary>
    /// ایجاد یک نمونه جدید از سرویس میزبان پردازشگر جریان فرآیند BPMN
    /// </summary>
    /// <param name="processor">پردازشگر جریان فرآیند BPMN</param>
    /// <param name="logger">سیستم ثبت وقایع</param>
    public BpmnProcessStreamProcessorHostedService(
        IServiceProvider serviceProvider,
        ILogger<BpmnProcessStreamProcessorHostedService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BPMN Process Stream Processor service is starting");

            var _processor = _serviceProvider.GetRequiredService<BpmnProcessStreamProcessor>();
        try
        {
            // شروع پردازشگر جریان
            await _processor.StartAsync(stoppingToken);
            
            // انتظار برای درخواست توقف
            while (!stoppingToken.IsCancellationRequested)
            {
                // بررسی اینکه آیا پردازشگر هنوز در حال اجراست
                if (!_processor.IsRunning)
                {
                    _logger.LogWarning("BPMN Process Stream Processor unexpectedly stopped. Attempting to restart...");
                    
                    try
                    {
                        // تلاش برای راه‌اندازی مجدد
                        await _processor.StartAsync(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to restart BPMN Process Stream Processor");
                    }
                }
                
                // کمی تأخیر قبل از بررسی مجدد وضعیت
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // این خطا طبیعی است زمانی که توکن لغو می‌شود
            _logger.LogInformation("BPMN Process Stream Processor service is stopping due to cancellation request");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in the BPMN Process Stream Processor service");
            throw;
        }
        finally
        {
            _logger.LogInformation("BPMN Process Stream Processor service is stopping");
            
            try
            {
                // اطمینان از توقف پردازشگر حتی در صورت خطا
                if (_processor.IsRunning)
                {
                    await _processor.StopAsync(CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping the BPMN Process Stream Processor");
            }
        }
    }
    
    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping BPMN Process Stream Processor service");
        
        // اطمینان از توقف پردازشگر
        
        await base.StopAsync(cancellationToken);
    }
} 