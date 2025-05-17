using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core;
using Novin.Bpmn.EventSourcing.Core.Deployments;
using Novin.Bpmn.EventSourcing.Core.EventStore;
using Novin.Bpmn.EventSourcing.Core.Executions;
using Novin.Bpmn.EventSourcing.Core.Topology;
using Novin.Bpmn.EventSourcing.Core.Process;
using Novin.Bpmn.EventSourcing.Core.Services;
using Novin.Bpmn.EventSourcing.Events;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.EventSourcing
{
    public static class BpmnEngineServiceCollectionExtensions
    {
        /// <summary>
        /// ثبت سرویس‌ها، استورها، هندلرها، و HostedServiceهای اصلی BPMN Engine در DI Container
        /// </summary>
        public static IServiceCollection AddBpmnEngine(this IServiceCollection services)
        {
            // Event Store (In-Memory)
            services.AddSingleton<IEventStore, InMemoryEventStore>();

            // Deployment Store (In-Memory)
            services.AddSingleton<IBpmnDeploymentStore, InMemoryBpmnDeploymentStore>();

            // Flow Topology Store (In-Memory)
            services.AddSingleton<IFlowTopologyStore, InMemoryFlowTopologyStore>();
            services.AddSingleton<IProcessStateStore, InMemoryProcessStateStore>();

            // Execution Context Repository & Rebuilder
            services.AddSingleton<IExecutionContextRepository, InMemoryExecutionContextRepository>();
            services.AddSingleton<IExecutionContextRebuilder, ExecutionContextRebuilder>();

            // Flow Topology Builder
            services.AddSingleton<IFlowTopologyBuilder, FlowTopologyBuilder>();

            // Deployment Service
            services.AddSingleton<IDeploymentService, DeploymentService>();
            services.AddSingleton<IJoinResolverService, JoinResolverService>();
            services.AddSingleton<IForkHandlerService, ForkHandlerService>();
            services.AddSingleton<IExecutionPathService, ExecutionPathService>();

            // Process Engine
            services.AddSingleton<IProcessEngine, ProcessEngine>();

            // Event Bus
            services.AddSingleton<IEventBus, EventBus>();

            // Handlers registration
            services.AddTransient<IBpmnEventHandler<ProcessStarted>, ProcessStartedEventHandler>();
            services.AddTransient<IBpmnEventHandler<ElementCompleted>, ElementCompletedEventHandler>();
            services.AddTransient<IBpmnEventHandler<ElementProcessing>, ElementProcessingEventHandler>();
            services.AddTransient<IBpmnEventHandler<ElementCreated>, ElementCreatedEventHandler>();
            services.AddTransient<IBpmnEventHandler<ProcessCompleted>,ProcessCompletedEventHandler>();
            services.AddTransient<IBpmnEventHandler<ProcessFailureEvent>,ProcessFailureEventHandler>();
            // هندلرهای دیگر را اینجا اضافه کنید    

            // Hosted Services (Background Workers)
            services.AddHostedService<EventWorkerService>();
            // اگر Worker های دیگری دارید اینجا اضافه کنید

            return services;
        }
        
        private static void RegisterAllEventHandlers(IServiceCollection services)
        {
            var handlerInterfaceType = typeof(IBpmnEventHandler<>);

            // گرفتن همه کلاس‌هایی که اینترفیس IBpmnEventHandler<> را پیاده‌سازی می‌کنند
            var handlerTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x=>x.GetTypes())
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .SelectMany(t => t.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterfaceType)
                    .Select(i => new { HandlerType = t, InterfaceType = i }))
                .ToList();

            foreach (var handler in handlerTypes)
            {
                services.AddTransient(handler.InterfaceType, handler.HandlerType);
            }
        }
    }
}
