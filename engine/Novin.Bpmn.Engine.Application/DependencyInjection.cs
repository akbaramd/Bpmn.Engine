using System.Reflection;
using MediatR.NotificationPublishers;
using Microsoft.Extensions.DependencyInjection;

// Orchestration
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.ElementHandlers;
using Novin.Bpmn.Engine.Application.EventHandlers;

// Dispatcher + element handlers

// Core services
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.DomainServices;

namespace Novin.Bpmn.Engine.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // MediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.NotificationPublisher = new TaskWhenAllPublisher();
        });
        services.AddMemoryCache();
        // -------------------------
        // Orchestration
        // -------------------------

        // -------------------------
        // BPMN runtime context
        // -------------------------
        services.AddScoped<IBpmnRuntimeContextFactory, BpmnRuntimeContextFactory>();

        // -------------------------
        // Dispatcher (Handler-based)
        // -------------------------
        services.AddScoped<INodeExecutionDispatcher, NodeExecutionDispatcher>();

        // -------------------------
        // FEEL
        // -------------------------
        services.AddSingleton<IFeelExpressionEvaluator, FeelExpressionEvaluator>();

        // -------------------------
        // IO Mapping (Process <-> Token)
        // -------------------------
        services.AddSingleton<IBonyanIoAccessor, BonyanIoAccessor>();
        services.AddScoped<IVariableMappingService, BonyanVariableMappingService>();

        // -------------------------
        // Core execution services (SRP)
        // -------------------------
        services.AddScoped<ITokenForkService, TokenForkService>();

        services.AddScoped<IGatewaySplitService, GatewaySplitService>();

        // اگر می‌خواهی شرط‌ها فقط روی Token.Variables اجرا شوند (پیشنهادی با IO Mapping)
        services.AddScoped<ISequenceFlowSelector,FeelSequenceFlowSelector>();

        services.AddScoped<IUserTaskService, UserTaskService>();

        // -------------------------
        // Process Completion Evaluation (BPMN2 semantics)
        // -------------------------

        // -------------------------
        // Process Status Service (Derived Status)
        // -------------------------

        // -------------------------
        // Process Execution Recorder (Minimal audit trail for executed nodes)
        // -------------------------

        // -------------------------
        // BPMN Error Boundary Finder
        // -------------------------
        services.AddScoped<IBpmnErrorBoundaryFinder, BpmnErrorBoundaryFinder>();

        // -------------------------
        // Token Management (Retry, Move, Terminate)
        // -------------------------

        // -------------------------
        // Incident Service
        // -------------------------
        services.AddScoped<IIncidentService, IncidentService>();
        services.AddScoped<IBpmnQuery, BpmnQuery>();
        services.AddScoped<IBpmnStartResolver, BpmnStartResolver>();
        services.AddScoped<IBoundaryEventSubscriptionService, IBoundaryEventSubscriptionService.BoundaryEventSubscriptionService>();
        services.AddScoped<IProcessInstantiationService, ProcessInstantiationService>();

        // -------------------------
        // Boundary Events
        // -------------------------
        services.AddScoped<IClock, SystemClock>();
        
        // Boundary Timer Scheduler: Use Quartz in production, Null for testing
        // To use Quartz: services.AddQuartz() in Program.cs and register QuartzBoundaryTimerScheduler
        // For now, using NullBoundaryTimerScheduler (can be swapped in Program.cs)
        services.AddScoped<IBoundaryTimerScheduler, NullBoundaryTimerScheduler>();

        // -------------------------
        // ScriptTask / ServiceTask
        // -------------------------

        // Options for MultiLanguageScriptTaskExecutor (چون ctor تو options می‌خواهد)
        services.AddSingleton(new MultiLanguageScriptTaskExecutorOptions
        {
            // مقادیر پیشنهادی؛ مطابق کلاس خودت تنظیم کن
            TreatNullFormatAsCSharp = true,
            CSharpTimeout = TimeSpan.FromSeconds(30),
            JavaScriptTimeout = TimeSpan.FromSeconds(30),
            JavaScriptMaxStatements = 10_000,
            JavaScriptMaxMemoryBytes = 16 * 1024 * 1024
        });

        services.AddSingleton<EmptyServiceTaskRegistry>();
        services.AddSingleton<IServiceTaskRegistry>(sp => sp.GetRequiredService<EmptyServiceTaskRegistry>());
        services.AddScoped<IServiceTaskExecutor, NullServiceTaskExecutor>();

        services.AddScoped<IScriptTaskExecutor, MultiLanguageScriptTaskExecutor>();

        // -------------------------
        // Client Communication Service
        // -------------------------
        services.AddScoped<IClientCommunicationService, SignalRClientCommunicationService>();

        // Background Services
        // -------------------------
        services.AddHostedService<WorkerMonitorBackgroundService>();

        // -------------------------
        // BPMN Element Handlers (با Decorator Pattern برای Variable Mapping)
        // -------------------------
        // ابتدا concrete handler‌ها را register می‌کنیم
        services.AddScoped<StartEventHandler>();
        services.AddScoped<EndEventHandler>();
        services.AddScoped<GatewayHandler>();
        services.AddScoped<UserTaskHandler>();
        services.AddScoped<ScriptTaskHandler>();
        services.AddScoped<ServiceTaskHandler>();
        services.AddScoped<DefaultFlowNodeHandler>();

        // Register handlers directly as IBpmnElementHandler (each handles its own variable mapping)
        services.AddScoped<IBpmnElementHandler, StartEventHandler>();
        services.AddScoped<IBpmnElementHandler, EndEventHandler>();
        services.AddScoped<IBpmnElementHandler, GatewayHandler>();
        services.AddScoped<IBpmnElementHandler, UserTaskHandler>();
        services.AddScoped<IBpmnElementHandler, ScriptTaskHandler>();
        services.AddScoped<IBpmnElementHandler, ServiceTaskHandler>();
        services.AddScoped<IBpmnElementHandler, DefaultFlowNodeHandler>();

        return services;
    }

}
