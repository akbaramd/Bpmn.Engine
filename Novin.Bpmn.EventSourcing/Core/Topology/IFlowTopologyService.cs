using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.EventSourcing.Core.Topology;

public interface IFlowTopologyService
{
    FlowTopology BuildAndStore(string deploymentKey, int version, BpmnDefinitions definitions);
    FlowTopology Build(BpmnDefinitions definitions);
}