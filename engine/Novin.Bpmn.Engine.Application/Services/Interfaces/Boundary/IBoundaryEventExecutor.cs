namespace Novin.Bpmn.Engine.Application.Services;

/// <summary>
/// Executor واحد برای semantics Boundary Event (BPMN2-سازگار)
/// این کلاس مسئولیت اجرای منطق interrupting/non-interrupting را دارد
/// </summary>
public interface IBoundaryEventExecutor
{
    /// <summary>
    /// Execute boundary event semantics:
    /// - اگر interrupting: cancel activity instance و create token در boundary
    /// - اگر non-interrupting: create token در boundary (بدون cancel کردن activity)
    /// </summary>
    Task ExecuteAsync(Guid subscriptionId, CancellationToken ct);
}
