using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.EventSourcing.Core.Topology;

public interface IFlowTopologyBuilder
{
    List<FlowTopology> Build(Guid deploymentId, BpmnDefinitions definitions);
    FlowTopology Build(Guid deploymentId, BpmnProcess process);
}