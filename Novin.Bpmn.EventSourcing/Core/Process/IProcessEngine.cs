namespace Novin.Bpmn.EventSourcing.Core.Process;

public interface IProcessEngine
{
    /// <summary>
    /// شروع پروسس جدید با شناسه‌های دیپلویمنت و پروسس و متغیرهای اولیه
    /// </summary>
    /// <param name="deploymentKey">کلید deployment</param>
    /// <param name="processId">شناسه فرآیند</param>
    /// <param name="initializeVariables">متغیرهای اولیه</param>
    /// <param name="startEventId">شناسه StartEvent که باید trigger شود. اگر null باشد، None StartEvent (یا اولین StartEvent) استفاده می‌شود.</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>ProcessState جاری پروسس</returns>
    Task<ProcessState> StartProcessAsync(string deploymentKey, string processId, Dictionary<string, object?>? initializeVariables = null, string? startEventId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// شروع یا ادامه اجرای پروسس از یک ProcessState موجود
    /// </summary>
    /// <returns>ProcessState جاری پروسس</returns>
    Task<ProcessState> StartProcessAsync(ProcessState state, CancellationToken cancellationToken = default);
}