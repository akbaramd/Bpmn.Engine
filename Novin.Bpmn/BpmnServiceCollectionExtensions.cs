using Microsoft.Extensions.DependencyInjection;
using Novin.Bpmn.Contracts;
using Novin.Bpmn.Core;
using Novin.Bpmn.V3;
using Novin.Bpmn.V3.Handlers.Gateways;
using Novin.Bpmn.V3.UserTasks;
using System;
using System.Linq;

namespace Novin.Bpmn
{
    public static class BpmnServiceCollectionExtensions
    {
        /// <summary>
        /// افزودن سرویس‌های موتور BPMN به کانتینر وابستگی
        /// </summary>
        public static IServiceCollection AddBpmnEngine(this IServiceCollection services)
        {
            // Register ScriptHandler - required for expression evaluation
            services.AddSingleton<ScriptHandler>();
            
            // Gateway handlers and router
            services.AddSingleton<BpmnV3ExclusiveGatewayHandler>(sp => 
                new BpmnV3ExclusiveGatewayHandler(sp.GetRequiredService<ScriptHandler>()));
            
            services.AddSingleton<BpmnV3InclusiveGatewayHandler>(sp => 
                new BpmnV3InclusiveGatewayHandler(sp.GetRequiredService<ScriptHandler>()));
            
            services.AddSingleton<BpmnV3ParallelGatewayHandler>();
            
            services.AddSingleton<BpmnV3GatewayRouter>(sp => 
                new BpmnV3GatewayRouter(
                    sp.GetRequiredService<BpmnV3ExclusiveGatewayHandler>(),
                    sp.GetRequiredService<BpmnV3InclusiveGatewayHandler>(),
                    sp.GetRequiredService<BpmnV3ParallelGatewayHandler>()
                ));
            
            // Register data accessors (default in-memory implementations)
            services.AddSingleton<IBpmnDefinitionAccessor, InMemoryBpmnDefinitionAccessor>();
            services.AddSingleton<IBpmnProcessInstanceAccessor, InMemoryBpmnProcessInstanceAccessor>();
            services.AddSingleton<IBpmnTaskAccessor, InMemoryBpmnTaskAccessor>();
            
            // Register managers
            services.AddSingleton<IBpmnDefinitionManager, BpmnDefinitionManager>();
            services.AddSingleton<IBpmnTaskManager, BpmnTaskManager>();
            
            // Register process manager
            services.AddSingleton<IBpmnProcessManager>(sp => {
                var gatewayRouter = sp.GetRequiredService<BpmnV3GatewayRouter>();
                var taskManager = sp.GetRequiredService<IBpmnTaskManager>();
                var instanceAccessor = sp.GetRequiredService<IBpmnProcessInstanceAccessor>();
                return new BpmnProcessManager(gatewayRouter, taskManager, instanceAccessor);
            });
            
            // Factory for creating process executors with specific instances
            services.AddTransient<Func<BpmnV3ProcessInstance, BpmnProcessManager>>(sp =>
                processInstance => {
                    var gatewayRouter = sp.GetRequiredService<BpmnV3GatewayRouter>();
                    var userTaskManager = sp.GetRequiredService<IBpmnTaskManager>();
                    var instanceAccessor = sp.GetRequiredService<IBpmnProcessInstanceAccessor>();
                    return new BpmnProcessManager(processInstance, gatewayRouter, userTaskManager, instanceAccessor);
                });
            
            // Register main BPMN engine
            services.AddSingleton<IBpmnEngine, BpmnEngine>();
            
            return services;
        }
        
        /// <summary>
        /// استفاده از پیاده‌سازی سفارشی برای دسترسی به تعاریف BPMN
        /// </summary>
        public static IServiceCollection AddCustomBpmnDefinitionAccessor<TAccessor>(this IServiceCollection services)
            where TAccessor : class, IBpmnDefinitionAccessor
        {
            // Remove existing registration and add new one
            services.RemoveAll<IBpmnDefinitionAccessor>();
            services.AddSingleton<IBpmnDefinitionAccessor, TAccessor>();
            return services;
        }
        
        /// <summary>
        /// استفاده از پیاده‌سازی سفارشی برای دسترسی به نمونه‌های فرآیند BPMN
        /// </summary>
        public static IServiceCollection AddCustomBpmnProcessInstanceAccessor<TAccessor>(this IServiceCollection services)
            where TAccessor : class, IBpmnProcessInstanceAccessor
        {
            // Remove existing registration and add new one
            services.RemoveAll<IBpmnProcessInstanceAccessor>();
            services.AddSingleton<IBpmnProcessInstanceAccessor, TAccessor>();
            return services;
        }
        
        /// <summary>
        /// استفاده از پیاده‌سازی سفارشی برای دسترسی به وظایف کاربری BPMN
        /// </summary>
        public static IServiceCollection AddCustomBpmnTaskAccessor<TAccessor>(this IServiceCollection services)
            where TAccessor : class, IBpmnTaskAccessor
        {
            // Remove existing registration and add new one
            services.RemoveAll<IBpmnTaskAccessor>();
            services.AddSingleton<IBpmnTaskAccessor, TAccessor>();
            return services;
        }
        
        // Helper extension method for removing all registrations of a service type
        private static IServiceCollection RemoveAll<TService>(this IServiceCollection services)
            where TService : class
        {
            var descriptors = services.Where(d => d.ServiceType == typeof(TService)).ToList();
            foreach (var descriptor in descriptors)
            {
                services.Remove(descriptor);
            }
            return services;
        }
    }
}