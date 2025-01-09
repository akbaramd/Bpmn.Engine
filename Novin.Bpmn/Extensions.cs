using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Core;
using Novin.Bpmn.Executors;
using Novin.Bpmn.V2;
using Novin.Bpmn.V2.Abstractions;
using Novin.Bpmn.V2.Handlers.Gateways;
using Novin.Bpmn.V2.Handlers.Tasks;
using Quartz;

namespace Novin.Bpmn
{
    public static class BpmnEngineExtensions
    {
        public static IServiceCollection AddBpmnEngine(this IServiceCollection services)
        {
            services.AddTransient<BpmnV2ProcessExecutor>();
            services.AddTransient<BpmnV2BoundaryEventHandler>();
            services.AddTransient<BpmnV2Router>();
            services.AddTransient<BpmnV2ExclusiveGatewayHandler>();
            services.AddTransient<BpmnV2InclusiveGatewayHandler>();
            services.AddTransient<BpmnV2ParallelGatewayHandler>();
            services.AddTransient<IBpmnV2TaskHandler,Bpmn2TaskHandler>();
            services.AddTransient<IBpmnV2ScriptTaskHandler,Bpmn2ScriptTaskHandler>();
            
            services.AddScoped<BpmnEngine>();
            services.AddScoped<ProcessStateManager>();
            services.AddScoped<BpmnProcessExecutor>();
            
            services.AddScoped<ScriptHandler>();
            services.AddScoped<IServiceTaskExecutor,ServiceTaskExecutor>();
            services.AddScoped<IUserTaskExecutor,UserTaskExecutor>();
            services.AddScoped<IScriptTaskExecutor, ScriptTaskExecutor>();
            // services.AddScoped<ITimerHandler, TimerHandler>();
            services.AddQuartz(q =>
            {
                // Register the TimerJob as durable
                // q.AddJob<TimerJob>(opts => opts.StoreDurably().WithIdentity("TimerJob"));
            });

            services.AddQuartzHostedService(options =>
            {
                options.WaitForJobsToComplete = true;
            });
            return services;
        }
    }
}