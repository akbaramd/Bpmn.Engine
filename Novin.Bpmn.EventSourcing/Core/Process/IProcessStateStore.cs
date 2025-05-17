namespace Novin.Bpmn.EventSourcing.Core.Process;

public interface IProcessStateStore
{
    ProcessState? Get(Guid instanceId);

    void Save(ProcessState state);

    void Remove(Guid instanceId);

    // برای کامپکشن: حذف استیت های قدیمی یا نسخه‌های تکراری
    void Compact();

    // (اختیاری) دریافت همه استیت‌ها برای عملیات مانیتورینگ یا مدیریت
    IEnumerable<ProcessState> GetAll();
    IEnumerable<ProcessState> GetByDeploymentKey(Guid deploymentKey);
}