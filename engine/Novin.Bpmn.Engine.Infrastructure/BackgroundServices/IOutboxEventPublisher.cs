namespace Novin.Bpmn.Engine.Infrastructure.BackgroundServices;

public interface IOutboxEventPublisher
{
    Task PublishAsync(OutboxEventEnvelope envelope, CancellationToken ct);
}