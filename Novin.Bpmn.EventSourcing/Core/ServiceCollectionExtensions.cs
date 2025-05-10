using Microsoft.Extensions.DependencyInjection;
using Novin.Bpmn.EventSourcing.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Novin.Bpmn.EventSourcing.Core;

/// <summary>
/// متدهای الحاقی برای ثبت سرویس‌های مرتبط با Event Sourcing
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// ثبت مخزن درون‌حافظه‌ای رویداد و گذرگاه رویداد
    /// </summary>
    /// <param name="services">مجموعه سرویس‌ها</param>
    /// <returns>مجموعه سرویس‌ها</returns>
    public static IServiceCollection AddBpmnEventSourcing(this IServiceCollection services)
    {
        services.AddSingleton<IEventStore, InMemoryEventStore>();
        services.AddSingleton<IStateStore, InMemoryStateStore>();
        services.AddSingleton<IEventBus, ServiceProviderEventBus>();
        
        return services;
    }
    
    /// <summary>
    /// ثبت تمام پردازش‌کننده‌های رویداد BPMN از مجموعه‌های مورد نظر
    /// </summary>
    /// <param name="services">مجموعه سرویس‌ها</param>
    /// <param name="assemblies">مجموعه‌های حاوی پردازش‌کننده‌ها</param>
    /// <returns>مجموعه سرویس‌ها</returns>
    public static IServiceCollection AddBpmnEventHandlers(this IServiceCollection services, params Assembly[] assemblies)
    {
        var assembliesToScan = assemblies.Length == 0 
            ? AppDomain.CurrentDomain.GetAssemblies() 
            : assemblies;
            
        // پیدا کردن تمام پیاده‌سازی‌های IBpmnEventHandler<>
        var handlerTypes = assembliesToScan
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .SelectMany(t => GetHandlerInterfaces(t))
            .ToList();
            
        // ثبت هر پردازش‌کننده در مجموعه سرویس‌ها
        foreach (var (implementationType, serviceType) in handlerTypes)
        {
            services.AddTransient(serviceType, implementationType);
        }
        
        return services;
    }
    
    private static IEnumerable<(Type ImplementationType, Type ServiceType)> GetHandlerInterfaces(Type type)
    {
        var handlerInterfaces = type.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IBpmnEventHandler<>));
            
        return handlerInterfaces.Select(i => (type, i));
    }
} 