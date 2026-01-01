# پیاده‌سازی Timer Boundary Events با Quartz

## ✅ کارهای انجام شده

### 1. Domain Layer
- ✅ `TimerType` enum (TimeDate, TimeDuration, TimeCycle)
- ✅ فیلدهای Timer به `BoundaryEventSubscription` اضافه شد:
  - `TimerType`, `TimerExpression`, `NextDueAtUtc`, `LastFiredAtUtc`, `FireCount`
- ✅ متدهای `MarkFired()`, `SetNextDueAt()`, `UpdateTimerInfo()`
- ✅ EF Core Configuration با Indexes برای Recovery

### 2. Infrastructure Layer
- ✅ `ITimerScheduler` interface
- ✅ `QuartzTimerScheduler` implementation
- ✅ `BoundaryTimerJob` (Quartz Job)
- ✅ `NullTimerScheduler` برای testing

### 3. Application Layer
- ✅ `BoundarySubscriptionCreatedEventHandler` → Schedule Quartz
- ✅ `BoundarySubscriptionCancelledEventHandler` → Unschedule Quartz
- ✅ DependencyInjection updated

## 🔧 کارهای باقی‌مانده

### 1. Configure Quartz در Program.cs

```csharp
// در Program.cs
builder.Services.AddQuartz(q =>
{
    q.UseMicrosoftDependencyInjection();
    
    // Register BoundaryTimerJob
    var jobKey = new JobKey("BoundaryTimerJob");
    q.AddJob<BoundaryTimerJob>(opts => opts.WithIdentity(jobKey));
});

builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

// Override ITimerScheduler با QuartzTimerScheduler
builder.Services.AddScoped<ITimerScheduler>(sp =>
{
    var scheduler = sp.GetRequiredService<IScheduler>();
    var logger = sp.GetRequiredService<ILogger<QuartzTimerScheduler>>();
    return new QuartzTimerScheduler(scheduler, logger);
});
```

### 2. آپدیت SubscribeBoundaryEvents برای Extract Timer Information

در `IBoundaryEventSubscriptionService.SubscribeBoundaryEvents` باید:

```csharp
// تشخیص نوع Boundary Event
var kind = DetermineBoundaryKind(boundaryEvent);

if (kind == BoundaryKind.Timer)
{
    var timerDef = boundaryEvent.Items.OfType<BpmnTimerEventDefinition>().FirstOrDefault();
    if (timerDef != null)
    {
        var (timerType, expression, dueAt, nextDueAt) = ParseTimerDefinition(
            timerDef, 
            node.StartedAtUtc ?? node.CreatedAtUtc);
        
        subscription = BoundaryEventSubscription.Create(
            // ... existing params ...
            timerType: timerType,
            timerExpression: expression,
            dueAt: dueAt,
            nextDueAtUtc: nextDueAt);
    }
}
```

### 3. ParseTimerDefinition Helper

```csharp
private static (TimerType, string, DateTimeOffset?, DateTimeOffset?) ParseTimerDefinition(
    BpmnTimerEventDefinition timerDef, 
    DateTime nodeStartedAt)
{
    // timeDate
    if (timerDef.TimeDate?.Text != null && timerDef.TimeDate.Text.Length > 0)
    {
        var dateStr = timerDef.TimeDate.Text[0];
        if (DateTimeOffset.TryParse(dateStr, out var date))
        {
            return (TimerType.TimeDate, dateStr, date, null);
        }
    }
    
    // timeDuration
    if (timerDef.TimeDuration?.Text != null && timerDef.TimeDuration.Text.Length > 0)
    {
        var durationStr = timerDef.TimeDuration.Text[0];
        if (System.Xml.XmlConvert.ToTimeSpan(durationStr) is var duration && duration != default)
        {
            var dueAt = nodeStartedAt.Add(duration);
            return (TimerType.TimeDuration, durationStr, dueAt, null);
        }
    }
    
    // timeCycle
    if (timerDef.TimeCycle?.Text != null && timerDef.TimeCycle.Text.Length > 0)
    {
        var cycleStr = timerDef.TimeCycle.Text[0];
        // Parse ISO-8601 repeat (R/PT5M or PT5M)
        var (interval, startAt) = ParseTimeCycle(cycleStr, nodeStartedAt);
        return (TimerType.TimeCycle, cycleStr, startAt, startAt);
    }
    
    throw new InvalidOperationException("Invalid timer definition");
}
```

### 4. آپدیت BoundarySubscriptionTriggeredEventHandler برای Cycle Timers

در `BoundarySubscriptionTriggeredEventHandler.Handle` بعد از spawn کردن boundary path:

```csharp
// اگر Timer cycle است و non-interrupting
if (sub.Kind == BoundaryKind.Timer && 
    sub.TimerType == TimerType.TimeCycle && 
    !sub.IsInterrupting)
{
    // Mark as fired (increment FireCount)
    sub.MarkFired();
    
    // Compute next due time
    var interval = ParseIntervalFromExpression(sub.TimerExpression);
    if (interval.HasValue)
    {
        var nextDueAt = DateTimeOffset.UtcNow.Add(interval.Value);
        sub.SetNextDueAt(nextDueAt);
        
        // Reschedule Quartz (via event)
        await _mediator.Publish(new BoundaryTimerRescheduleRequestedEvent(
            SubscriptionId: sub.Id,
            NextDueAt: nextDueAt), ct);
    }
    
    await _uow.BoundarySubscriptions.UpdateAsync(sub, ct);
}
// else: one-shot timer → MarkTriggered (already done)
```

### 5. Handler برای NodeCompletedEvent → Cancel Timers

```csharp
public class NodeCompletedDomainEventHandler : INotificationHandler<NodeCompletedDomainEvent>
{
    // Cancel all active timer subscriptions for this ActivityInstanceId
    var subscriptions = await _subscriptionRepo.GetActiveByActivityInstanceIdAsync(
        notification.ActivityInstanceId, ct);
    
    foreach (var sub in subscriptions.Where(s => s.Kind == BoundaryKind.Timer))
    {
        sub.Cancel("Host activity completed");
        await _subscriptionRepo.UpdateAsync(sub, ct);
        // Unschedule will happen via BoundarySubscriptionCancelledEventHandler
    }
}
```

### 6. Recovery Mechanism (Startup)

```csharp
public class TimerRecoveryService : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        // Load all active Timer subscriptions without ExternalJobKey
        var subscriptions = await _subscriptionRepo.GetActiveTimersWithoutJobKeyAsync(ct);
        
        foreach (var sub in subscriptions)
        {
            // Verify Quartz job doesn't exist
            if (!await _timerScheduler.ExistsAsync(sub.Id, ct))
            {
                // Reschedule
                if (sub.TimerType == TimerType.TimeCycle && sub.NextDueAtUtc.HasValue)
                {
                    var interval = ParseIntervalFromExpression(sub.TimerExpression);
                    if (interval.HasValue)
                        await _timerScheduler.ScheduleIntervalAsync(
                            sub.Id, sub.NextDueAtUtc.Value, interval.Value, ct);
                }
                else if (sub.DueAt.HasValue)
                {
                    await _timerScheduler.ScheduleOnceAsync(sub.Id, sub.DueAt.Value, ct);
                }
            }
        }
    }
}
```

## 📝 نکات مهم

1. **Outbox-first**: Quartz scheduling بعد از commit transaction انجام می‌شود (via Outbox)
2. **Idempotent Keys**: `("bpmn.boundary.timer", subscriptionId.ToString())`
3. **Cycle Timers**: برای non-interrupting cycle timers، `MarkFired()` استفاده می‌شود نه `MarkTriggered()`
4. **Recovery**: Index روی `(Kind, State, DueAt)` و `(Kind, State, NextDueAtUtc)` برای recovery

## 🧪 Testing

برای testing بدون Quartz:
- `NullTimerScheduler` استفاده می‌شود (default)
- Timer events manually trigger می‌شوند

