using System.Reflection;
using MediatR.NotificationPublishers;
using Microsoft.Extensions.DependencyInjection;

// Orchestration
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.EventHandlers;

// Dispatcher + element handlers
using Novin.Bpmn.Engine.Application.Execution;
using Novin.Bpmn.Engine.Application.Execution.Strategies;

// Core services
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Application.Services.Feel;

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

        // -------------------------
        // Orchestration
        // -------------------------
        services.AddScoped<ITokenProcessingOrchestrator, TokenProcessingOrchestrator>();

        // -------------------------
        // BPMN runtime context
        // -------------------------
        services.AddScoped<IBpmnRuntimeContextFactory, BpmnRuntimeContextFactory>();

        // -------------------------
        // Dispatcher (Handler-based)
        // -------------------------
        services.AddScoped<ITokenExecutionDispatcher, TokenExecutionDispatcher>();

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
        services.AddScoped<ITokenNavigationService, TokenNavigationService>();
        services.AddScoped<ITokenForkService, TokenForkService>();

        services.AddScoped<IGatewayJoinService, GatewayJoinService>();
        services.AddScoped<IGatewaySplitService, GatewaySplitService>();

        // اگر می‌خواهی شرط‌ها فقط روی Token.Variables اجرا شوند (پیشنهادی با IO Mapping)
        services.AddScoped<ISequenceFlowSelector,FeelSequenceFlowSelector>();

        services.AddScoped<IUserTaskService, UserTaskService>();

        // -------------------------
        // Process Completion Evaluation (BPMN2 semantics)
        // -------------------------
        services.AddScoped<IProcessCompletionEvaluator, ProcessCompletionEvaluator>();

        // -------------------------
        // Process Status Service (Derived Status)
        // -------------------------
        services.AddScoped<IProcessStatusService, ProcessStatusService>();

        // -------------------------
        // BPMN Error Boundary Finder
        // -------------------------
        services.AddScoped<IBpmnErrorBoundaryFinder, BpmnErrorBoundaryFinder>();

        // -------------------------
        // Token Management (Retry, Move, Terminate)
        // -------------------------
        services.AddScoped<ITokenManagementService, TokenManagementService>();

        // -------------------------
        // Incident Service
        // -------------------------
        services.AddScoped<IIncidentService, IncidentService>();

        // -------------------------
        // Boundary Events
        // -------------------------
        services.AddScoped<IBoundaryEventExecutor, BoundaryEventExecutor>();
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
            CSharpTimeout = TimeSpan.FromSeconds(2),
            JavaScriptTimeout = TimeSpan.FromSeconds(1),
            JavaScriptMaxStatements = 10_000,
            JavaScriptMaxMemoryBytes = 16 * 1024 * 1024
        });

        services.AddSingleton<EmptyServiceTaskRegistry>();
        services.AddSingleton<IServiceTaskRegistry>(sp => sp.GetRequiredService<EmptyServiceTaskRegistry>());
        services.AddScoped<IServiceTaskExecutor, NullServiceTaskExecutor>();

        services.AddScoped<IScriptTaskExecutor, MultiLanguageScriptTaskExecutor>();

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

        // سپس هر کدام را با VariableMappingDecorator wrap می‌کنیم و به عنوان IBpmnElementHandler register می‌کنیم
        services.AddScoped<IBpmnElementHandler>(sp => CreateDecoratedHandler<StartEventHandler>(sp));
        services.AddScoped<IBpmnElementHandler>(sp => CreateDecoratedHandler<EndEventHandler>(sp));
        services.AddScoped<IBpmnElementHandler>(sp => CreateDecoratedHandler<GatewayHandler>(sp));
        services.AddScoped<IBpmnElementHandler>(sp => CreateDecoratedHandler<UserTaskHandler>(sp));
        services.AddScoped<IBpmnElementHandler>(sp => CreateDecoratedHandler<ScriptTaskHandler>(sp));
        services.AddScoped<IBpmnElementHandler>(sp => CreateDecoratedHandler<ServiceTaskHandler>(sp));
        services.AddScoped<IBpmnElementHandler>(sp => CreateDecoratedHandler<DefaultFlowNodeHandler>(sp));

        return services;
    }

    /// <summary>
    /// Helper method برای ساخت decorated handler.
    /// این متد از Decorator Pattern استفاده می‌کند تا Variable Mapping را به صورت خودکار اضافه کند.
    /// </summary>
    private static IBpmnElementHandler CreateDecoratedHandler<THandler>(IServiceProvider sp)
        where THandler : IBpmnElementHandler
    {
        var innerHandler = sp.GetRequiredService<THandler>();
        var mappingService = sp.GetRequiredService<IVariableMappingService>();
        var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<VariableMappingElementHandlerDecorator>>();

        return new VariableMappingElementHandlerDecorator(innerHandler, mappingService, logger);
    }
}
