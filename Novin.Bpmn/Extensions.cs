using Microsoft.Extensions.DependencyInjection;
using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Core;
using Novin.Bpmn.Executors;
using Novin.Bpmn.Executors.Abstracts;
using Novin.Bpmn.Handlers;
using Quartz;

namespace Novin.Bpmn.Blazor
{
    public static class BpmnEngineExtensions
    {
        public static IServiceCollection AddBpmnEngine(this IServiceCollection services)
        {
            services.AddScoped<BpmnEngine>();
            services.AddScoped<ScriptHandler>();
            services.AddScoped<IServiceTaskExecutor,ServiceTaskExecutor>();
            services.AddScoped<IUserTaskExecutor,UserTaskExecutor>();
            services.AddScoped<IScriptTaskExecutor, ScriptTaskExecutor>();
            services.AddScoped<ITimerHandler, TimerHandler>();
            services.AddQuartz(q =>
            {
                // Register the TimerJob as durable
                q.AddJob<TimerJob>(opts => opts.StoreDurably().WithIdentity("TimerJob"));
            });

            services.AddQuartzHostedService(options =>
            {
                options.WaitForJobsToComplete = true;
            });
            return services;
        }
    }
}