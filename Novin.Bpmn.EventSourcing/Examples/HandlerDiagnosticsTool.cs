using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing.Examples
{
    /// <summary>
    /// ابزار تشخیصی برای بررسی ثبت صحیح هندلرها
    /// </summary>
    public static class HandlerDiagnosticsTool
    {
        /// <summary>
        /// اجرای تشخیص ثبت هندلرها
        /// </summary>
        public static async Task RunAsync()
        {
            Console.WriteLine("=== تشخیص ثبت هندلرها ===");
            
            // ایجاد سرویس‌ها
            var services = new ServiceCollection();
            
            // افزودن لاگر
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });
            
            // افزودن سرویس‌های BPMN Event Sourcing
            services.AddBpmnEventSourcing();
            
            // ثبت هندلرها از اسمبلی فعلی
            services.AddBpmnEventHandlers(typeof(HandlerDiagnosticsTool).Assembly);
            
            // ساخت سرویس‌پروایدر
            var serviceProvider = services.BuildServiceProvider();
            
            // بررسی ثبت هندلرها
            Console.WriteLine("\n=== بررسی ثبت هندلرها برای انواع رویداد ===");
            
            // جستجوی همه رویدادها
            var eventTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => !t.IsAbstract && !t.IsInterface && 
                           (typeof(IBpmnEvent).IsAssignableFrom(t) || t.GetInterfaces().Contains(typeof(IBpmnEvent))))
                .ToList();
                
            Console.WriteLine($"یافتن {eventTypes.Count} نوع رویداد در اسمبلی‌های بارگذاری شده.");
            
            foreach (var eventType in eventTypes)
            {
                // بررسی هندلرهای ثبت شده برای هر نوع رویداد
                Console.WriteLine($"\nبررسی هندلرهای {eventType.Name}:");
                
                var handlerType = typeof(IBpmnEventHandler<>).MakeGenericType(eventType);
                var handlers = serviceProvider.GetServices(handlerType).ToList();
                
                if (handlers.Any())
                {
                    Console.WriteLine($"- {handlers.Count} هندلر ثبت شده:");
                    foreach (var handler in handlers)
                    {
                        Console.WriteLine($"  * {handler.GetType().FullName}");
                    }
                }
                else
                {
                    Console.WriteLine($"- هیچ هندلری برای {eventType.Name} ثبت نشده است.");
                    
                    // بررسی پیاده‌سازی‌های هندلر
                    var implementations = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => a.GetTypes())
                        .Where(t => !t.IsAbstract && !t.IsInterface && t.IsClass)
                        .Where(t => t.GetInterfaces()
                                    .Any(i => i.IsGenericType && 
                                             i.GetGenericTypeDefinition() == typeof(IBpmnEventHandler<>) &&
                                             i.GetGenericArguments()[0] == eventType))
                        .ToList();
                        
                    if (implementations.Any())
                    {
                        Console.WriteLine($"  * {implementations.Count} پیاده‌سازی یافت شد اما ثبت نشده است:");
                        foreach (var impl in implementations)
                        {
                            Console.WriteLine($"    - {impl.FullName}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("  * هیچ پیاده‌سازی هندلر یافت نشد.");
                    }
                }
            }
            
            // بررسی اسمبلی‌های بارگذاری شده
            Console.WriteLine("\n=== اسمبلی‌های بارگذاری شده ===");
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !a.GlobalAssemblyCache)
                .OrderBy(a => a.FullName))
            {
                try
                {
                    Console.WriteLine($"- {assembly.FullName}, مسیر: {assembly.Location}");
                }
                catch
                {
                    Console.WriteLine($"- {assembly.FullName}, مسیر: [نامشخص]");
                }
            }
            
            Console.WriteLine("\n=== پایان تشخیص ===");
            
            await Task.CompletedTask;
        }
    }
} 