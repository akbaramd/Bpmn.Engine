using System;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Novin.Bpmn.Engine.Infrastructure.BackgroundServices;
using Novin.Bpmn.Engine.Infrastructure.Outbox.MassTransit;
using Novin.Bpmn.Engine.Infrastructure.Persistence;

namespace Novin.Bpmn.Engine.Infrastructure.Outbox.Redis;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMessageBrockers(this IServiceCollection services, IConfiguration cfg)
    {
        var partitions = cfg.GetValue("Outbox:Partitions", 16);
        var rabbit = cfg.GetConnectionString("RabbitMq") ?? "rabbitmq://guest:guest@localhost:5672/";

        services.AddMassTransit(x =>
        {
            x.AddConsumer<OutboxEventConsumer>();
            x.AddEntityFrameworkOutbox<BpmnEngineDbContext>(o =>
            {
                // یکی را انتخاب کن:
                o.UsePostgres();    // اگر Npgsql داری
                // o.UseSqlServer();
                // o.UseMySql();

                o.UseBusOutbox();

                // Tune برای latency کم (با احتیاط)
                o.QueryDelay = TimeSpan.FromMilliseconds(25);
                o.QueryMessageLimit = 500;
            });
            x.AddConfigureEndpointsCallback((context, name, cfg) =>
            {
                cfg.UseEntityFrameworkOutbox<BpmnEngineDbContext>(context);
            });
            x.UsingRabbitMq((ctx, busCfg) =>
            {
                busCfg.Host(new Uri(rabbit));

                for (var p = 0; p < partitions; p++)
                {
                    var partition = p;

                    busCfg.ReceiveEndpoint(OutboxQueueNames.PartitionQueue(partition), e =>
                    {
                        e.PrefetchCount = 32;
                        e.ConcurrentMessageLimit = 1; // Zeebe-like per-partition ordering

                        // اگر خواستی retry خود MT:
                        // e.UseMessageRetry(r => r.Interval(5, TimeSpan.FromMilliseconds(200)));

                        e.ConfigureConsumer<OutboxEventConsumer>(ctx);
                    });
                }
            });
        });

        services.AddSingleton<IOutboxEventPublisher>(sp =>
            new OutboxEventPublisher(sp.GetRequiredService<IBus>(), partitions));

        return services;
    }
}