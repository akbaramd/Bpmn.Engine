using Novin.Bpmn.EventSourcing.Core.Topology;

namespace Novin.Bpmn.EventSourcing.Core.Services.Gateway;

/// <summary>
/// پیاده‌سازی Factory برای GatewayBehavior
/// </summary>
public class GatewayBehaviorFactory : IGatewayBehaviorFactory
{
    public IGatewayBehavior CreateBehavior(FlowNode gatewayNode)
    {
        if (gatewayNode == null)
            throw new ArgumentNullException(nameof(gatewayNode));

        if (!gatewayNode.IsGateway)
            throw new ArgumentException($"Node '{gatewayNode.ElementId}' is not a Gateway.", nameof(gatewayNode));

        var elementType = gatewayNode.ElementType;

        // تشخیص نوع Gateway بر اساس ElementType
        if (elementType.Contains("exclusiveGateway", StringComparison.OrdinalIgnoreCase) ||
            elementType.Contains("ExclusiveGateway", StringComparison.OrdinalIgnoreCase))
        {
            return new ExclusiveGatewayBehavior();
        }

        if (elementType.Contains("parallelGateway", StringComparison.OrdinalIgnoreCase) ||
            elementType.Contains("ParallelGateway", StringComparison.OrdinalIgnoreCase))
        {
            return new ParallelGatewayBehavior();
        }

        if (elementType.Contains("inclusiveGateway", StringComparison.OrdinalIgnoreCase) ||
            elementType.Contains("InclusiveGateway", StringComparison.OrdinalIgnoreCase))
        {
            return new InclusiveGatewayBehavior();
        }

        if (elementType.Contains("eventBasedGateway", StringComparison.OrdinalIgnoreCase) ||
            elementType.Contains("EventBasedGateway", StringComparison.OrdinalIgnoreCase))
        {
            return new EventBasedGatewayBehavior();
        }

        // Default: اگر نوع Gateway مشخص نبود، Exclusive را استفاده کن
        return new ExclusiveGatewayBehavior();
    }
}

