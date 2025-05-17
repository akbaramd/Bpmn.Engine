using Novin.Bpmn.EventSourcing.Core.Deployments;

public interface IBpmnDeploymentStore
{
    /// <summary>
    /// ثبت یک deployment جدید. نسخه جدید به صورت خودکار افزایش می‌یابد.
    /// </summary>
    BpmnDeployment Store(string deploymentKey, string bpmnXml);

    /// <summary>
    /// دریافت یک deployment بر اساس کلید و نسخه مشخص.
    /// </summary>
    BpmnDeployment? Get(string deploymentKey, string version);

    /// <summary>
    /// دریافت آخرین نسخه‌ی یک deployment با کلید مشخص.
    /// </summary>
    BpmnDeployment? GetLatest(string deploymentKey);

    /// <summary>
    /// دریافت همه نسخه‌های موجود برای یک deployment.
    /// </summary>
    IReadOnlyList<BpmnDeployment> GetAllVersions(string deploymentKey);
    IReadOnlyList<BpmnDeployment> GetAllVersions();

    /// <summary>
    /// دریافت deployment با شناسه یکتا.
    /// </summary>
    BpmnDeployment? GetById(Guid deploymentId);

    /// <summary>
    /// بررسی وجود یک deployment مشخص.
    /// </summary>
    bool Exists(Guid deploymentId);
}