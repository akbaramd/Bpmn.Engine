using System.Xml.Serialization;
using Novin.Bpmn.EventSourcing.Core.Deployments;
using Novin.Bpmn.EventSourcing.Core.Topology;
using Novin.Bpmn.Models.Models;

public class DeploymentService : IDeploymentService
{
    private readonly IBpmnDeploymentStore _deploymentStore;
    private readonly IFlowTopologyBuilder _topologyBuilder;
    private readonly IFlowTopologyStore _topologyStore;

    public DeploymentService(
        IBpmnDeploymentStore deploymentStore,
        IFlowTopologyBuilder topologyBuilder,
        IFlowTopologyStore topologyStore)
    {
        _deploymentStore = deploymentStore;
        _topologyBuilder = topologyBuilder;
        _topologyStore = topologyStore;
    }

    public BpmnDeployment Deploy(string deploymentKey, string bpmnXml)
    {
        // 1. ذخیره نسخه جدید از BPMN XML
        var deployment = _deploymentStore.Store(deploymentKey, bpmnXml);

        // 2. تبدیل XML به BpmnDefinitions
        var definitions = DeserializeXml<BpmnDefinitions>(bpmnXml);

        // 3. ساخت توپولوژی‌ها
        var topologies = _topologyBuilder.Build(deployment.DeploymentId, definitions);

        // 4. ذخیره هر توپولوژی در TopologyStore
        foreach (var topology in topologies)
            _topologyStore.Save(topology);

        return deployment;
    }

    public BpmnDeploymentDetails? GetDeploymentWithTopology(Guid deploymentId)
    {
        var deployment = _deploymentStore.GetById(deploymentId);
        if (deployment == null)
            return null;

        var topologies = _topologyStore.GetAllByDeployment(deploymentId);

        return new BpmnDeploymentDetails
        {
            Deployment = deployment,
            Topologies = topologies.ToList()
        };
    }

    private static T DeserializeXml<T>(string xml)
    {
        var serializer = new XmlSerializer(typeof(T));
        using var reader = new StringReader(xml);
        return (T)serializer.Deserialize(reader)!;
    }
}