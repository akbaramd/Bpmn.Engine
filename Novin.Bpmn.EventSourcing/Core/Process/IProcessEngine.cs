namespace Novin.Bpmn.EventSourcing.Core.Process;

public interface IProcessEngine
{
    /// <summary>
    /// شروع پروسس جدید با شناسه‌های دیپلویمنت و پروسس و متغیرهای اولیه
    /// </summary>
    /// <returns>ProcessState جاری پروسس</returns>
    Task<ProcessState> StartProcessAsync(string deploymentKey, string processId, Dictionary<string, object?>? initializeVariables = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// شروع یا ادامه اجرای پروسس از یک ProcessState موجود
    /// </summary>
    /// <returns>ProcessState جاری پروسس</returns>
    Task<ProcessState> StartProcessAsync(ProcessState state, CancellationToken cancellationToken = default);
}