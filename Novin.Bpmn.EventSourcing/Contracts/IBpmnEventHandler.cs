using System.Threading;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing.Contracts;

/// <summary>
/// رابط پایه برای پردازش‌کننده رویدادهای BPMN
/// </summary>
/// <typeparam name="TEvent">نوع رویداد BPMN برای پردازش</typeparam>
public interface IBpmnEventHandler<in TEvent> where TEvent : IBpmnEvent
{
    /// <summary>
    /// پردازش رویداد
    /// </summary>
    /// <param name="event">رویداد ورودی</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>عملیات غیرهمزمان</returns>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
} 