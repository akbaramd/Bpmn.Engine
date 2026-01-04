using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Novin.Bpmn.Engine.Application.Services.Interfaces.Infrastructure;
using Novin.Bpmn.Engine.Infrastructure.Outbox.Direct;
using StackExchange.Redis;

namespace Novin.Bpmn.Engine.Infrastructure.Outbox.Redis;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRedisOutbox(this IServiceCollection services,IConfiguration cfg)
    {
      
        var cs = cfg.GetConnectionString("Redis") ?? "localhost:6379";

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(cs));

        var opt = new RedisOutboxQueueOptions();
        cfg.GetSection("Outbox:Redis").Bind(opt);
        services.AddSingleton(opt);

        services.AddSingleton<IOutboxQueue, RedisOutboxQueue>();

        // Relay: SQL -> Redis streams
        // services.AddHostedService<OutboxRelayHostedService>();
        // services.AddHostedService<RedisStreamOutboxConsumerHostedService>();
         services.AddHostedService<DirectOutboxProcessorHostedService>();

        // Consumers: Redis streams -> MediatR

        return services;
    }
}
