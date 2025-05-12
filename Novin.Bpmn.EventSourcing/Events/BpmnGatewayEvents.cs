// GatewayEvents.cs

using Novin.Bpmn.EventSourcing.Events;
using System.Collections.Generic;

namespace Novin.Bpmn.EventSourcing.Events
{
    // ===========================
    // Exclusive Gateway Events
    // ===========================
    public record ExclusiveGatewayCreated : ElementCreated
    {
        public override string EventType => nameof(ExclusiveGatewayCreated);

        /// <summary>
        /// شناسه‌های جریان ورودی که باید قبل از ادامه ادغام شوند
        /// استفاده در مرحله merge: منتظر توکن از هر جریان ورودی باشید.
        /// </summary>
        public List<string> IncomingFlowIds { get; init; } = new();

        /// <summary>
        /// اگر هیچ شرطی برقرار نشد، از این جریان خروجی استفاده می‌شود
        /// استفاده در fork: مسیر پیش‌فرض زمانی که هیچ شرط true نیست.
        /// </summary>
        public string? DefaultFlowId { get; init; }

        /// <summary>
        /// نگاشت شناسه جریان خروجی به عبارت شرطی برای ارزیابی
        /// استفاده در fork: هر جریان تنها اگر شرط‌اش true باشد گرفته شود.
        /// </summary>
        public Dictionary<string, string> Conditions { get; init; } = new();
    }

    public record ExclusiveGatewayProcessing : ElementProcessing
    {
        public override string EventType => nameof(ExclusiveGatewayProcessing);
        // در ExclusiveGateway، پردازش فقط شامل بررسی شرایط است که در Completed انجام می‌شود.
    }

    public record ExclusiveGatewayCompleted : ElementCompleted
    {
        public override string EventType => nameof(ExclusiveGatewayCompleted);

        /// <summary>
        /// شناسه جریان‌های خروجی که براساس شرط‌ها گرفته شده‌اند
        /// استفاده در fork: لیست مسیرهای انتخاب‌شده.
        /// </summary>
        public List<string> TakenFlowIds { get; init; } = new();
    }

    // ===========================
    // Parallel Gateway Events
    // ===========================
    public record ParallelGatewayCreated : ElementCreated
    {
        public override string EventType => nameof(ParallelGatewayCreated);

        /// <summary>
        /// شناسه‌های جریان ورودی که باید قبل از ادامه ادغام شوند
        /// استفاده در merge: منتظر توکن از همه شاخه‌ها باشید.
        /// </summary>
        public List<string> IncomingFlowIds { get; init; } = new();

        /// <summary>
        /// تعداد توکن‌های مورد انتظار تا merge کامل شود
        /// استفاده در merge: وقتی تعداد توکن‌های رسیده برابر با این مقدار باشد.
        /// </summary>
        public int ExpectedIncomingCount { get; init; }
    }

    public record ParallelGatewayProcessing : ElementProcessing
    {
        public override string EventType => nameof(ParallelGatewayProcessing);
        // در ParallelGateway، پردازش با شمارش توکن‌ها مدیریت می‌شود.
    }

    public record ParallelGatewayCompleted : ElementCompleted
    {
        public override string EventType => nameof(ParallelGatewayCompleted);

        /// <summary>
        /// شناسه‌های جریان خروجی که توکن روی آن‌ها توزیع شد
        /// استفاده در fork: ایجاد توکن برای هر خروجی.
        /// </summary>
        public List<string> OutgoingFlowIds { get; init; } = new();

        /// <summary>
        /// تعداد توکن‌های توزیع‌شده
        /// استفاده در fork: تعداد واقعی توکنی که از این گیت‌وی عبور کرد.
        /// </summary>
        public int DispatchedTokenCount { get; init; }
    }

    // ===========================
    // Inclusive Gateway Events
    // ===========================
    public record InclusiveGatewayCreated : ElementCreated
    {
        public override string EventType => nameof(InclusiveGatewayCreated);

        /// <summary>
        /// شناسه‌های جریان ورودی که باید قبل از ادامه ادغام شوند
        /// استفاده در merge: منتظر توکن از هر ورودی باشید.
        /// </summary>
        public List<string> IncomingFlowIds { get; init; } = new();

        /// <summary>
        /// نگاشت شناسه جریان خروجی به عبارت شرطی برای ارزیابی
        /// استفاده در merge: شرط‌ها همزمان بررسی می‌شوند.
        /// </summary>
        public Dictionary<string, string> Conditions { get; init; } = new();
    }

    public record InclusiveGatewayProcessing : ElementProcessing
    {
        public override string EventType => nameof(InclusiveGatewayProcessing);
        // در InclusiveGateway، پردازش بدون انتشار رویداد جدید است.
    }

    public record InclusiveGatewayCompleted : ElementCompleted
    {
        public override string EventType => nameof(InclusiveGatewayCompleted);

        /// <summary>
        /// شناسه‌های جریان خروجی که براساس شرط‌ها یا merge انتخاب شدند
        /// استفاده در fork: ترکیبی از merge و شرط‌ها.
        /// </summary>
        public List<string> TakenFlowIds { get; init; } = new();
    }
}