using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core;
using Novin.Bpmn.EventSourcing.Events;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing.Examples;

/// <summary>
/// مثال استفاده از Event Sourcing در BPMN
/// </summary>
public class BpmnEventSourcingExample
{
    /// <summary>
    /// اجرای مثال
    /// </summary>
    public static async Task RunAsync()
    {
        // ساخت سرویسها
        var host = CreateHostBuilder().Build();
        
        // شروع خدمات پس‌زمینه از جمله پردازشگر رویداد
        await host.StartAsync();
        
        try
        {
            // دریافت سرویس‌های مورد نیاز
            var eventBus = host.Services.GetRequiredService<IEventBus>();
            var eventStore = host.Services.GetRequiredService<IEventStore>();
            var stateStore = host.Services.GetRequiredService<IStateStore>();
            var logger = host.Services.GetRequiredService<ILogger<BpmnEventSourcingExample>>();
            
            // ایجاد یک نمونه فرآیند
            var processInstanceId = Guid.NewGuid().ToString();
            var processDefinitionId = "SampleProcess";
            
            logger.LogInformation("Creating process instance {ProcessInstanceId}", processInstanceId);
            
            // نمایش وضعیت اولیه
            logger.LogInformation("Initial state store contains {Count} instances", 
                ((InMemoryStateStore)stateStore).Count);
            
            // انتشار رویدادهای ایجاد فرآیند
            await eventBus.PublishAsync(new ProcessInstanceCreating
            {
                ProcessInstanceId = processInstanceId,
                ProcessDefinitionId = processDefinitionId,
                DeploymentKey = "sample-deployment",
                DefinitionXml = "<bpmn:definitions>...</bpmn:definitions>",
                InitialVariables = new Dictionary<string, object>
                {
                    { "requestId", Guid.NewGuid().ToString() },
                    { "amount", 1000 }
                }
            });
            
            // کمی صبر برای پردازش رویداد
            await Task.Delay(200);
            
            // تأیید ایجاد فرآیند
            await eventBus.PublishAsync(new ProcessInstanceCreated
            {
                ProcessInstanceId = processInstanceId,
                ProcessDefinitionId = processDefinitionId,
                Variables = new Dictionary<string, object>
                {
                    { "requestId", Guid.NewGuid().ToString() },
                    { "amount", 1000 }
                }
            });
            
            // کمی صبر برای پردازش رویداد
            await Task.Delay(200);
            
            // نمایش وضعیت پس از ایجاد فرآیند
            logger.LogInformation("After instance creation, state store contains {Count} instances", 
                ((InMemoryStateStore)stateStore).Count);
            
            // بررسی وضعیت فرآیند
            var state = await stateStore.GetStateAsync<BpmnProcessState>(processInstanceId);
            if (state != null)
            {
                logger.LogInformation("Process instance state exists with status: {Status}", state.Status);
            }
            else
            {
                logger.LogWarning("Process instance state not found!");
            }
            
            // شروع فرآیند با یک رویداد شروع
            await eventBus.PublishAsync(new ProcessInstanceStarting
            {
                ProcessInstanceId = processInstanceId,
                StartEventId = "StartEvent_1"
            });
            
            // کمی صبر برای پردازش رویداد
            await Task.Delay(200);
            
            // تأیید شروع فرآیند
            await eventBus.PublishAsync(new ProcessInstanceStarted
            {
                ProcessInstanceId = processInstanceId
            });
            
            // کمی صبر برای پردازش رویداد
            await Task.Delay(200);
            
            // فعال‌سازی یک المان در فرآیند (مثلاً یک وظیفه)
            await eventBus.PublishAsync(new ElementActivating
            {
                ProcessInstanceId = processInstanceId,
                ElementId = "Task_1",
                ElementType = "bpmn:UserTask"
            });
            
            // کمی صبر برای پردازش رویداد
            await Task.Delay(200);
            
            // تأیید فعال‌سازی المان
            await eventBus.PublishAsync(new ElementActivated
            {
                ProcessInstanceId = processInstanceId,
                ElementId = "Task_1",
                ElementType = "bpmn:UserTask"
            });
            
            // کمی صبر برای پردازش رویداد
            await Task.Delay(200);
            
            // بررسی وضعیت فرآیند
            state = await stateStore.GetStateAsync<BpmnProcessState>(processInstanceId);
            if (state != null)
            {
                logger.LogInformation("Process instance has {ActiveCount} active elements and {CompletedCount} completed elements",
                    state.ActiveElements.Count, state.CompletedElements.Count);
            }
            
            // تکمیل المان
            await eventBus.PublishAsync(new ElementCompleting
            {
                ProcessInstanceId = processInstanceId,
                ElementId = "Task_1",
                ElementType = "bpmn:UserTask",
                UpdatedVariables = new Dictionary<string, object>
                {
                    { "approved", true }
                }
            });
            
            // کمی صبر برای پردازش رویداد
            await Task.Delay(200);
            
            // تأیید تکمیل المان
            await eventBus.PublishAsync(new ElementCompleted
            {
                ProcessInstanceId = processInstanceId,
                ElementId = "Task_1",
                ElementType = "bpmn:UserTask"
            });
            
            // کمی صبر برای پردازش رویداد
            await Task.Delay(200);
            
            // تکمیل فرآیند
            await eventBus.PublishAsync(new ProcessInstanceCompleting
            {
                ProcessInstanceId = processInstanceId,
                EndEventId = "EndEvent_1",
                FinalVariables = new Dictionary<string, object>
                {
                    { "result", "SUCCESS" }
                }
            });
            
            // کمی صبر برای پردازش رویداد
            await Task.Delay(200);
            
            // نهایی کردن تکمیل فرآیند
            await eventBus.PublishAsync(new ProcessCompletedEvent
            {
                ProcessInstanceId = processInstanceId,
                EndEventId = "EndEvent_1"
            });
            
            // کمی صبر برای پردازش رویداد
            await Task.Delay(200);
            
            // بررسی وضعیت نهایی فرآیند
            state = await stateStore.GetStateAsync<BpmnProcessState>(processInstanceId);
            if (state != null)
            {
                logger.LogInformation("Final process instance status: {Status}", state.Status);
                logger.LogInformation("Process instance has {ActiveCount} active elements and {CompletedCount} completed elements",
                    state.ActiveElements.Count, state.CompletedElements.Count);
            }
            
            logger.LogInformation("Example execution completed");
        }
        finally
        {
            // توقف خدمات پس‌زمینه
            await host.StopAsync();
        }
    }
    
    /// <summary>
    /// ساخت میزبان برنامه
    /// </summary>
    private static IHostBuilder CreateHostBuilder()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            })
            .ConfigureServices((_, services) =>
            {
                // ثبت سرویس‌های Event Sourcing
                services.AddBpmnEventSourcing();
                
                // جستجوی خودکار پردازش‌کننده‌های رویداد در اسمبلی فعلی
                services.AddBpmnEventHandlers(typeof(BpmnEventSourcingExample).Assembly);
            });
    }
} 