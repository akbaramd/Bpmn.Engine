using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services.Interfaces.Infrastructure;
using Novin.Bpmn.Engine.Domain.Common;
using Novin.Bpmn.Engine.Infrastructure.BackgroundServices;

namespace Novin.Bpmn.Engine.Infrastructure.Outbox.MassTransit;

public sealed class OutboxEventConsumer : IConsumer<OutboxEventEnvelope>
{
    private readonly IMediator _mediator;
    private readonly IJsonSerializer _json;
    private readonly ILogger<OutboxEventConsumer> _logger;

    private static readonly ConcurrentDictionary<string, Type?> TypeCache = new(StringComparer.Ordinal);

    public OutboxEventConsumer(
        IMediator mediator,
        IJsonSerializer json,
        ILogger<OutboxEventConsumer> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _json = json ?? throw new ArgumentNullException(nameof(json));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(ConsumeContext<OutboxEventEnvelope> ctx)
    {
        var env = ctx.Message;
        var now = DateTime.UtcNow;

        try
        {
            var domainEvent = Deserialize(env.MessageType, env.Payload);
            if (domainEvent is null)
                throw new InvalidOperationException("Domain event deserialization failed.");

            // ⚠️ IMPORTANT: handlers must be idempotent (OutboxId may retry)
            await _mediator.Publish(domainEvent, ctx.CancellationToken).ConfigureAwait(false);


            _logger.LogDebug("[OUTBOX-MT] OK OutboxId={OutboxId} Name={Name}", env.OutboxId, env.MessageName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[OUTBOX-MT] FAIL OutboxId={OutboxId} Name={Name}", env.OutboxId, env.MessageName);

        }
    }

    private IDomainEvent? Deserialize(string messageType, string payload)
    {
        if (string.IsNullOrWhiteSpace(messageType)) return null;

        var t = TypeCache.GetOrAdd(messageType, static mt => Type.GetType(mt));
        if (t is null) return null;

        return _json.DeserializeObject(payload ?? "{}", t) as IDomainEvent;
    }
}
