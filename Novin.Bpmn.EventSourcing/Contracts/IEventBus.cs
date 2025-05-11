using System;
using System.Threading;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing.Contracts;

/// <summary>
/// گذرگاه رویداد برای انتشار و اشتراک‌گذاری رویدادها در معماری Event Sourcing
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// انتشار یک رویداد
    /// </summary>
    /// <param name="event">رویداد برای انتشار</param>
    /// <param name="cancellationToken">توکن لغو</param>
    Task PublishAsync(IBpmnEvent @event, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// انتشار یک رویداد با نوع مشخص
    /// </summary>
    /// <typeparam name="TEvent">نوع رویداد</typeparam>
    /// <param name="event">رویداد برای انتشار</param>
    /// <param name="cancellationToken">توکن لغو</param>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IBpmnEvent;
    

} 