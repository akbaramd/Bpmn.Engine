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
        var deployment = _deploymentStore.Store(deploymentKey, bpmnXml);
        var definitions = DeserializeXml<BpmnDefinitions>(bpmnXml);
        var topologies = _topologyBuilder.Build(deployment.DeploymentId, definitions);

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

    public List<BpmnDeploymentDetails> GetAll()
    {
        // فرض: متد GetAllDeployments در IBpmnDeploymentStore اضافه شده و همه نسخه‌ها را برمی‌گرداند
        var allDeployments = _deploymentStore.GetAllVersions();

        var result = new List<BpmnDeploymentDetails>();

        foreach (var deployment in allDeployments)
        {
            var topologies = _topologyStore.GetAllByDeployment(deployment.DeploymentId).ToList();

            result.Add(new BpmnDeploymentDetails
            {
                Deployment = deployment,
                Topologies = topologies
            });
        }

        return result;
    }

    private static T DeserializeXml<T>(string xml)
    {
        var serializer = new XmlSerializer(typeof(T));
        using var reader = new StringReader(xml);
        return (T)serializer.Deserialize(reader)!;
    }
}
