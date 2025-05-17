using System.Collections;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Events;

namespace Novin.Bpmn.EventSourcing.Core.EventStore;

/// <summary>
/// قرارداد ذخیره‌سازی فقط‌افزایشی برای رویدادهای BPMN.
/// </summary>
public interface IEventStore
{
    // ذخیره رویداد جدید به همراه Payload و وضعیت اولیه
    void Append(EventEntity eventEntity);
    void Append(BpmnEvent eventEntity);

    // دریافت رویدادها بر اساس InstanceId و فیلتر وضعیت (اختیاری)
    IReadOnlyList<EventEntity> GetEvents(Guid instanceId, EventStatus[]? statuses = null);

    // به‌روزرسانی وضعیت رویداد
    void UpdateStatus(Guid eventId, EventStatus newStatus, string? errorMessage = null,int? retryCount = null);

    // دریافت همه رویدادها (اختیاری با فیلتر وضعیت)
    IReadOnlyList<EventEntity> GetAll(EventStatus[]? statuses = null);
    IReadOnlyList<EventEntity> GetIncompletedEvents(int size);
}

public enum EventStatus
{
    Pending,   // آماده ارسال یا پردازش
    Sent,      // با موفقیت ارسال شده
    Failed     // ارسال یا پردازش با خطا مواجه شده
}

public class EventEntity
{
    public Guid EventId { get; set; }
    public Guid InstanceId { get; set; } 
    public string EventType { get; set; } = null!;
    public DateTime Timestamp { get; set; }

    public string Payload { get; set; } = null!;  // JSON

    public string TypeFullName { get; set; } = null!;  // Namespace + ClassName
    public string AssemblyName { get; set; } = null!;  // نام اسمبلی

    public EventStatus Status { get; set; } = EventStatus.Pending;
    public int RetryCount { get; set; } = 0;
    public string? ErrorMessage { get; set; }
}
