using Novin.Bpmn.EventSourcing.Core.Deployments;

public interface IDeploymentService
{
    /// <summary>
    /// ثبت و deploy کردن یک مدل BPMN جدید (نسخه جدید ایجاد می‌شود)
    /// </summary>
    BpmnDeployment Deploy(string deploymentKey, string bpmnXml);

    /// <summary>
    /// دریافت یک deployment خاص به همراه توپولوژی‌های آن
    /// </summary>
    BpmnDeploymentDetails? GetDeploymentWithTopology(Guid deploymentId);
}

public class BpmnDeploymentDetails
{
    public BpmnDeployment Deployment { get; init; } = default!;
    public List<FlowTopology> Topologies { get; init; } = new();
}