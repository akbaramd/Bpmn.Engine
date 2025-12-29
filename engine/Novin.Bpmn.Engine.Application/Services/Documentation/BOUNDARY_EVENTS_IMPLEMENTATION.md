# بررسی پیاده‌سازی Boundary Events

## ✅ موارد پیاده‌سازی شده (مطابق با توصیه‌ها)

### 1. Boundary Subscription Runtime ✅
- `BoundaryEventSubscription` entity ایجاد شده
- تمام فیلدهای مورد نیاز (Timer, Message, Error, ActivityInstanceId) وجود دارد
- Version برای optimistic concurrency

### 2. TokenMovedEvent Handler ✅
- `BoundarySubscriptionManager` handler برای `TokenMovedEvent` ایجاد شده
- وقتی token وارد activity می‌شود → subscription‌ها ایجاد می‌شوند
- وقتی token از activity خارج می‌شود → subscription‌ها cancel می‌شوند
- Handler برای `TokenCompletedEvent` و `TokenTerminatedEvent` هم وجود دارد

### 3. ActivityInstanceId ✅
- `ActivityInstanceId` به Token اضافه شده (جدا از ScopeId)
- وقتی token وارد activity می‌شود (UserTask, SubProcess, ...) → ActivityInstanceId set می‌شود
- در `BoundarySubscriptionManager.CreateSubscriptionsForElementAsync` انجام می‌شود

### 4. IBoundaryTimerScheduler ✅
- Interface قابل تعویض ایجاد شده
- `NullBoundaryTimerScheduler` برای testing/development
- آماده برای Hangfire/Quartz implementation

### 5. IBoundaryEventExecutor ✅
- Executor واحد برای semantics BPMN2
- منطق interrupting/non-interrupting در یک جا
- Cancel activity instance برای interrupting events
- Cancel همه subscription‌های دیگر در activity instance

### 6. TriggerBoundarySubscriptionCommand ✅
- Command برای trigger کردن boundary events
- از scheduler یا message bus صدا زده می‌شود
- از `IBoundaryEventExecutor` استفاده می‌کند

### 7. Error Boundary از Executor مشترک ✅
- در `HandleBpmnErrorAsync` از `IBoundaryEventExecutor` استفاده می‌شود
- یکپارچگی با سایر boundary events

---

## ⚠️ موارد نیازمند بهبود (TODO)

### 1. Scheduling بعد از Commit (Outbox Pattern)
**مشکل فعلی:**
- Timer scheduling داخل transaction انجام می‌شود
- اگر transaction rollback شود، job ممکن است schedule شده باشد

**راه حل:**
```csharp
// Domain Event
public sealed record BoundaryTimerSubscriptionCreatedEvent(
    Guid SubscriptionId,
    DateTimeOffset DueAt,
    DateTime OccurredAtUtc
) : IDomainEvent;

// Handler که بعد از commit schedule می‌کند
public sealed class BoundaryTimerSubscriptionCreatedEventHandler
    : INotificationHandler<BoundaryTimerSubscriptionCreatedEvent>
{
    // Schedule بعد از commit
}
```

### 2. Repository Implementation
- `EfBoundarySubscriptionRepository` باید پیاده‌سازی شود
- Migration برای `BoundarySubscriptions` table
- Migration برای `ActivityInstanceId` در `Tokens` table

### 3. GetActiveByActivityInstanceAsync
**مشکل:** این متد نیاز به query بر اساس ActivityInstanceId دارد که در subscription ذخیره شده است.

**راه حل:** در repository implementation:
```csharp
public async Task<IEnumerable<BoundaryEventSubscription>> GetActiveByActivityInstanceAsync(
    Guid activityInstanceId, 
    CancellationToken ct)
{
    return await _context.BoundarySubscriptions
        .Where(s => s.ActivityInstanceId == activityInstanceId 
                 && s.State == SubscriptionState.Active)
        .ToListAsync(ct);
}
```

### 4. IBoundaryTimerScheduler در Executor
**مشکل:** در `BoundaryEventExecutor` برای cancel کردن external jobs، scheduler inject نشده است.

**راه حل:** IBoundaryTimerScheduler را به constructor اضافه کنید.

### 5. TokenState.Canceled (اختیاری)
**پیشنهاد:** به جای استفاده از `Terminated` با reason "CanceledByBoundaryEvent"، می‌توانید `TokenState.Canceled` اضافه کنید.

---

## 📋 چک‌لیست نهایی

- [x] BoundaryEventSubscription entity
- [x] ActivityInstanceId در Token
- [x] BoundarySubscriptionManager (TokenMovedEvent handler)
- [x] IBoundaryTimerScheduler interface
- [x] IBoundaryEventExecutor (semantics واحد)
- [x] TriggerBoundarySubscriptionCommand
- [x] Error boundary از executor مشترک
- [ ] Repository implementation
- [ ] Migration
- [ ] Outbox pattern برای scheduling
- [ ] IBoundaryTimerScheduler در Executor
- [ ] Dependency Injection registration

---

## 🎯 نکات مهم

1. **Subscription در TokenMovedEvent**: ✅ انجام می‌شود
2. **ActivityInstanceId جدا از ScopeId**: ✅ انجام می‌شود
3. **Executor واحد**: ✅ انجام می‌شود
4. **Scheduling قابل تعویض**: ✅ انجام می‌شود
5. **Cancel activity instance**: ✅ انجام می‌شود (با ActivityInstanceId)

---

## 🔄 جریان کامل

```
Token وارد Activity
    ↓
TokenMovedEvent
    ↓
BoundarySubscriptionManager
    ↓
Set ActivityInstanceId (اگر activity است)
    ↓
Create BoundarySubscriptions
    ↓
Schedule Timer (فعلاً داخل Tx - باید Outbox شود)
    ↓
Commit Transaction
    ↓
[Outbox] Schedule Timer (بعد از commit)
    ↓
Timer Trigger (از Hangfire/Quartz)
    ↓
TriggerBoundarySubscriptionCommand
    ↓
BoundaryEventExecutor.ExecuteAsync
    ↓
Interrupting: Cancel Activity Instance + Create Boundary Token
Non-Interrupting: Create Boundary Token (بدون cancel)
```
