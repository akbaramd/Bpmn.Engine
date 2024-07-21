using Microsoft.Extensions.DependencyInjection;

namespace Novin.Bpmn.Blazor
{
    public static class BpmnEngineExtensions
    {
        public static IServiceCollection AddBpmnEngine(this IServiceCollection services)
        {
            // Add your BPMN engine services here
            // services.AddTransient<IBpmnService, BpmnService>();
            return services;
        }
    }
}