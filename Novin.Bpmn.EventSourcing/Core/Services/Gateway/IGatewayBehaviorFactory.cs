using Novin.Bpmn.EventSourcing.Core.Topology;

namespace Novin.Bpmn.EventSourcing.Core.Services.Gateway;

/// <summary>
/// Factory برای ایجاد GatewayBehavior مناسب بر اساس نوع Gateway
/// </summary>
public interface IGatewayBehaviorFactory
{
    /// <summary>
    /// ایجاد GatewayBehavior مناسب بر اساس ElementType
    /// </summary>
    IGatewayBehavior CreateBehavior(FlowNode gatewayNode);
}

