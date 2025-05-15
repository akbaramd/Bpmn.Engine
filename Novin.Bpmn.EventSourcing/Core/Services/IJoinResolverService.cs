using Novin.Bpmn.EventSourcing.Core.Executions;
using Novin.Bpmn.EventSourcing.Core.Topology;
using ExecutionContext = Novin.Bpmn.EventSourcing.Core.Executions.ExecutionContext;

public interface IJoinResolverService
{
    /// <summary>
    /// بررسی می‌کند که آیا شاخه‌های لازم برای ادامه اجرای Join آماده هستند یا خیر.
    /// </summary>
    /// <param name="topology">توپولوژی فرآیند</param>
    /// <param name="joinNodeId">شناسه نقطه Join</param>
    /// <param name="executionContexts">لیست Contextهای فعال</param>
    /// <returns>آیا می‌توان ادغام کرد و ادامه داد</returns>
    bool CanJoin(FlowTopology topology, string joinNodeId, IEnumerable<ExecutionContext> executionContexts);

    /// <summary>
    /// عملیات ادغام شاخه‌های ExecutionContext و تولید Context واحد ادامه اجرا
    /// </summary>
    /// <param name="topology">توپولوژی فرآیند</param>
    /// <param name="joinNodeId">شناسه نقطه Join</param>
    /// <param name="executionContexts">لیست Contextهای مرتبط</param>
    /// <returns>Context ادغام شده برای ادامه اجرا</returns>
    ExecutionContext MergeContexts(FlowTopology topology, string joinNodeId, IEnumerable<ExecutionContext> executionContexts);
}