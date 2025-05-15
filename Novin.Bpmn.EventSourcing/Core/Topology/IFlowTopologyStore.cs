public interface IFlowTopologyStore
{
    /// <summary>
    /// ذخیره توپولوژی برای یک Deployment مشخص.
    /// </summary>
    void Save(FlowTopology topology);

    /// <summary>
    /// دریافت توپولوژی برای یک Deployment مشخص و فرآیند.
    /// </summary>
    FlowTopology? Get(Guid deploymentId, string processId);

    /// <summary>
    /// دریافت تمام توپولوژی‌های مرتبط با یک Deployment.
    /// </summary>
    IReadOnlyList<FlowTopology> GetAllByDeployment(Guid deploymentId);

    /// <summary>
    /// بررسی وجود توپولوژی برای یک Deployment و فرآیند خاص.
    /// </summary>
    bool Exists(Guid deploymentId, string processId);
}