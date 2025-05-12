using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nest;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core;
using Novin.Bpmn.EventSourcing.Core.EventHandlers;
using Novin.Bpmn.EventSourcing.Events;
using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing;

/// <summary>
/// متدهای توسعه برای ثبت سرویس‌های Event Sourcing در BPMN
/// </summary>
public static class BpmnEventSourcingServiceCollectionExtensions
{
    /// <summary>
    /// افزودن سرویس‌های BPMN Event Sourcing به کانتینر DI
    /// </summary>
    /// <param name="services">مجموعه سرویس‌ها</param>
    /// <param name="configuration">تنظیمات اضافی (اختیاری)</param>
    /// <returns>مجموعه سرویس‌ها</returns>
    public static IServiceCollection AddBpmnEventSourcing(
        this IServiceCollection services,
        Action<BpmnEventSourcingOptions>? configuration = null)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        
        // پیکربندی تنظیمات
        var options = new BpmnEventSourcingOptions();
        configuration?.Invoke(options);
        
        // پایه Event Sourcing
        services.AddSingleton<IEventStore, ElasticsearchEventStore>();
        services.AddSingleton<IEventSerializer, JsonEventSerializer>();
        services.AddSingleton<IProcessDeploymentStore, ElasticsearchProcessDeploymentStore>();
        services.AddSingleton<IEventBus, ServiceProviderEventBus>();
        services.AddSingleton<IProcessInstanceStateStore, ElasticsearchProcessInstanceStateStore>();

        
        // سرویس BpmnProcessor
        services.AddSingleton<BpmnService>();
        

        // سرویس UserTask
        services.AddSingleton<IUserTaskStore, InMemoryUserTaskStore>();
        
        // تنها ثبت هندلرها اگر گزینه خودکار ثبت هندلرها فعال نیست
        if (!options.AutoRegisterEventHandlers)
        {
            // ثبت پردازش‌کننده‌های رویداد پایه
            services.AddTransient<IBpmnEventHandler<ElementCreated>, ElementCreatedHandler>();
            services.AddTransient<IBpmnEventHandler<ElementCompleted>, ElementCompletedHandler>();
            services.AddTransient<IBpmnEventHandler<ElementProcessing>, ElementProcessingHandler>();
            
            // ثبت هندلرهای وظایف کاربری
            
            // ثبت پردازش‌کننده‌های گیت‌وی
        }
        else
        {
            // ثبت خودکار هندلرها از اسمبلی فعلی
            // استفاده از اسمبلی فعلی به جای اسمبلی اجرایی
            var assembly = typeof(BpmnEventSourcingServiceCollectionExtensions).Assembly;
            services.AddBpmnEventHandlers(assembly);
        }
        
        
        return services;
    }

    /// <summary>
    /// افزودن سرویس‌های Elasticsearch به کانتینر DI
    /// </summary>
    /// <param name="services">مجموعه سرویس‌ها</param>
    /// <param name="configuration">تنظیمات Elasticsearch</param>
    /// <returns>مجموعه سرویس‌ها</returns>
    public static IServiceCollection AddElasticsearch(
        this IServiceCollection services,
        Action<ElasticsearchOptions> configuration)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        var options = new ElasticsearchOptions();
        configuration(options);

        var settings = new ConnectionSettings(new Uri(options.Url))
            .DefaultIndex(options.IndexPrefix)
            .EnableDebugMode()
            .PrettyJson()
            .RequestTimeout(options.ConnectionTimeout)
            .MaximumRetries(options.MaxRetries);

        if (options.EnableSsl)
        {
            settings.EnableApiVersioningHeader();
            if (!options.VerifySsl)
            {
                settings.ServerCertificateValidationCallback((sender, certificate, chain, sslPolicyErrors) => true);
            }
        }

        if (!string.IsNullOrEmpty(options.Username) && !string.IsNullOrEmpty(options.Password))
        {
            settings.BasicAuthentication(options.Username, options.Password);
        }

        var client = new ElasticClient(settings);
        services.AddSingleton<IElasticClient>(client);

        return services;
    }
    
    /// <summary>
    /// ثبت پردازش‌کننده‌های رویداد BPMN از اسمبلی‌های مشخص شده
    /// </summary>
    /// <param name="services">مجموعه سرویس‌ها</param>
    /// <param name="assemblies">اسمبلی‌هایی که باید جستجو شوند</param>
    /// <returns>مجموعه سرویس‌ها</returns>
    public static IServiceCollection AddBpmnEventHandlers(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (assemblies == null || assemblies.Length == 0)
        {
            throw new ArgumentException("At least one assembly must be provided", nameof(assemblies));
        }
        
        foreach (var assembly in assemblies)
        {
            // یافتن همه کلاس‌های پیاده‌سازی رابط IBpmnEventHandler<>
            var handlerTypes = assembly
                .GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface && t.IsClass)
                .Where(t => t.GetInterfaces()
                            .Any(i => i.IsGenericType && 
                                      i.GetGenericTypeDefinition() == typeof(IBpmnEventHandler<>)))
                .ToList();
            
            // ثبت هر پردازش‌کننده با نوع واسط متناظر آن
            foreach (var handlerType in handlerTypes)
            {
                var interfaceTypes = handlerType.GetInterfaces()
                    .Where(i => i.IsGenericType && 
                                i.GetGenericTypeDefinition() == typeof(IBpmnEventHandler<>))
                    .ToList();
                
                foreach (var interfaceType in interfaceTypes)
                {
                    services.AddTransient(interfaceType, handlerType);
                }
            }
        }
        
        return services;
    }
}

/// <summary>
/// تنظیمات Event Sourcing برای BPMN
/// </summary>
public class BpmnEventSourcingOptions
{
    /// <summary>
    /// آیا پردازش‌کننده‌های رویداد به صورت خودکار ثبت شوند
    /// </summary>
    public bool AutoRegisterEventHandlers { get; set; } = true;
    
    /// <summary>
    /// مسیر ذخیره‌سازی وضعیت
    /// </summary>
    public string? StateStorePath { get; set; }
    
    /// <summary>
    /// مسیر ذخیره‌سازی رویدادها
    /// </summary>
    public string? EventStorePath { get; set; }
    
    /// <summary>
    /// مسیر ذخیره‌سازی تعاریف BPMN
    /// </summary>
    public string? DefinitionsDirectory { get; set; }
}

