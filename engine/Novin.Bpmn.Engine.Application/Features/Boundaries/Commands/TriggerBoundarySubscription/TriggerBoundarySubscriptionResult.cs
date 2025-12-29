namespace Novin.Bpmn.Engine.Application.Commands.TriggerBoundarySubscription;

public class TriggerBoundarySubscriptionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? NewTokenId { get; set; }
}