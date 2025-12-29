# جریان مدیریت Exception و BPMN Error در BPMN Engine

این سند توضیح می‌دهد که **دقیقاً چه اتفاقی می‌افتد** وقتی یک exception یا BPMN error در یک نود رخ می‌دهد.

**⚠️ مهم**: در BPMN 2.0، دو نوع خطا وجود دارد که باید جداگانه handle شوند:

1. **BPMN Error** (Business Error): خطای business که باید توسط Error Boundary / Error EventSubprocess catch شود
2. **Technical Exception** (Technical Failure): خطای تکنیکی/سیستمی که باید به Incident تبدیل شود

---

## 📋 خلاصه سریع

| سوال | BPMN Error | Technical Failure |
|------|------------|-------------------|
| **آیا توکن Fail می‌شود؟** | ✅ **بله** - توکن به `TokenState.Failed` تبدیل می‌شود | ✅ **بله** - توکن به `TokenState.Failed` تبدیل می‌شود |
| **آیا پروسس ادامه می‌دهد؟** | ❌ **خیر** - پروسس **تکمیل نمی‌شود** تا زمانی که همه توکن‌های Live تمام شوند | ❌ **خیر** - پروسس **تکمیل نمی‌شود** تا زمانی که همه توکن‌های Live تمام شوند |
| **آیا Join منتظر می‌ماند؟** | ✅ **بله** - اگر یک شاخه Failed شود، Join منتظر می‌ماند | ✅ **بله** - اگر یک شاخه Failed شود، Join منتظر می‌ماند |
| **آیا Incident ایجاد می‌شود؟** | ✅ **بله** - یک Incident با `ErrorType.BpmnError` و `Status=Open` ایجاد می‌شود | ✅ **بله** - یک Incident با `ErrorType.TechnicalFailure` و `Status=Open` ایجاد می‌شود |
| **Error Code** | ✅ **بله** - Error code از BPMN model (مثلاً `"INVALID_AMOUNT"`) | ❌ **خیر** - errorCode = null |
| **Stack Trace** | ❌ **خیر** - stackTrace = null | ✅ **بله** - stack trace ذخیره می‌شود |
| **Propagation** | 🔄 باید توسط Error Boundary / Error EventSubprocess catch شود (در حال پیاده‌سازی) | ❌ Propagation نمی‌شود - به Incident تبدیل می‌شود |

---

## 🔴 مسیر 1: BPMN Error (Business Error)

BPMN Error یک خطای business است که توسط مدل BPMN تعریف شده است. این خطا باید توسط Error Boundary یا Error EventSubprocess catch شود.

### مرحله 1: BPMN Error در نود throw می‌شود

```
[ScriptTask: validateAmount]
  ↓
  throw new BpmnErrorException("INVALID_AMOUNT", "Amount exceeds limit")
  ↓
[VariableMappingElementHandlerDecorator] catch
  ↓
  throw (propagate به بالا - بدون wrap)
```

**نکته مهم**: 
- `BpmnErrorException` **wrap نمی‌شود** - مستقیماً propagate می‌شود
- ❌ Token هنوز Fail نشده است
- ❌ Incident ایجاد نشده است
- ✅ Transaction اول (Tx1) rollback می‌شود

### مرحله 2: Orchestrator BPMN Error را Catch می‌کند

```csharp
// در TokenProcessingOrchestrator.ProcessAsync()

try {
    await _uow.ExecuteInTransactionAsync(async trxCt => {
        // اجرای نود (Tx1)
        await _dispatcher.DispatchAsync(...);
    });
}
catch (BpmnErrorException bex) {
    // ✅ BPMN Error را catch می‌کنیم
    await HandleBpmnErrorAsync(processId, tokenId, bex, ct);
}
```

**نکته مهم**: 
- Transaction اول (Tx1) **rollback شده است**
- هیچ تغییری در دیتابیس ذخیره نشده است
- BPMN Error به یک transaction **جداگانه** (Tx2) منتقل می‌شود

### مرحله 3: HandleBpmnErrorAsync (Tx2 - Transaction جداگانه)

```csharp
private async Task HandleBpmnErrorAsync(...) {
    await _uow.ExecuteInTransactionAsync(async trxCt => {
        // 1. ایجاد Incident با ErrorType.BpmnError
        var incident = await _incidentService.CreateBpmnErrorAsync(
            processId, tokenId, elementId, bex.Code, bex.Message, trxCt);
        
        // 2. Fail کردن Token با ErrorType.BpmnError
        token.Fail(
            $"BPMN Error: {bex.Message}",
            ErrorType.BpmnError,
            errorCode: bex.Code,  // ✅ Error code ذخیره می‌شود
            incident.Id);
        
        // 3. ذخیره تغییرات
        await _uow.SaveChangesAsync(trxCt);
    });
    
    // ✅ هیچ throw نمی‌کنیم - exception handle شده است
    // 🔄 TODO: در آینده باید Error Boundary / Error EventSubprocess را پیدا کنیم
    // و Token را به آنجا منتقل کنیم
}
```

**نتیجه این مرحله**:
- ✅ **Incident ایجاد می‌شود** با `ErrorType.BpmnError` و `Status = Open`
- ✅ **Token به `TokenState.Failed` تبدیل می‌شود** با `ErrorType.BpmnError`
- ✅ **Error Code ذخیره می‌شود** (مثلاً `"INVALID_AMOUNT"`)
- ✅ **TokenFailedEvent** منتشر می‌شود
- ✅ همه چیز در دیتابیس **ذخیره می‌شود** (Tx2 commit می‌شود)

### مرحله 4: TokenFailedEvent Handler

```csharp
// TokenFailedEventHandler
public async Task Handle(TokenFailedEvent evt, CancellationToken ct) {
    // ProcessCompletionEvaluator را صدا می‌زند
    await _evaluator.EvaluateCompletionAsync(evt.ProcessId, ct);
}
```

**نکته**: این handler بلافاصله بعد از Fail شدن توکن اجرا می‌شود.

### مرحله 5: ProcessCompletionEvaluator بررسی می‌کند

```csharp
// در ProcessCompletionEvaluator.EvaluateCompletionAsync()

// 1. همه توکن‌ها را می‌گیرد
var allTokens = await _uow.Tokens.GetByProcessIdAsync(processId, ct);

// 2. توکن‌های Live را شناسایی می‌کند
var liveTokens = tokensList
    .Where(t => IsLiveToken(t))  // Active, Waiting, Failed
    .ToList();

// 3. Open Incidents را بررسی می‌کند
var openIncidents = await _uow.Incidents.GetByProcessIdAndStatusAsync(
    processId, IncidentStatus.Open, ct);

// 4. قانون Completion:
if (liveTokens.Count == 0 && openIncidents.Count == 0) {
    process.Complete();  // ✅ پروسس تمام می‌شود
}
else {
    // ❌ پروسس ادامه می‌دهد (Running باقی می‌ماند)
}
```

**نکته مهم**: 
- `Failed` توکن‌ها (چه BPMN Error چه Technical Failure) **Live** محسوب می‌شوند
- اگر یک `Failed` توکن با `Open` Incident وجود داشته باشد، پروسس **تکمیل نمی‌شود**

### مرحله 6: رفتار Join Gateway

```csharp
// در GatewayJoinService.TryJoinAsync()

// 1. Tokens که به join رسیده‌اند (Waiting state)
var waiting = allTokens
    .Where(t =>
        t.CurrentElementId == arrivingToken.CurrentElementId &&
        t.State == TokenState.Waiting &&
        t.ScopeId == scopeId)
    .ToList();

// 2. ArrivedViaFlowId: کلیدهای ورودی که tokens از طریق آن‌ها به join رسیده‌اند
var arrivedKeys = waiting
    .Select(t => t.ArrivedViaFlowId)
    .Where(x => !string.IsNullOrWhiteSpace(x))
    .Distinct()
    .ToList();

// 3. قانون BPMN: Join فقط بر اساس arrivals تصمیم‌گیری می‌کند
if (arrivedKeys.Count < expectedCount) {
    // هنوز همه‌ی شاخه‌های مورد انتظار به join نرسیده‌اند
    return true;  // Still waiting
}

// Merge: همه شاخه‌های مورد انتظار رسیده‌اند
```

**نتیجه**:
- Join **فقط بر اساس arrivals تصمیم‌گیری می‌کند** (ArrivedViaFlowId)
- اگر یک شاخه Failed شود و هنوز به join نرسیده باشد:
  - Join منتظر می‌ماند تا `arrivedKeys.Count >= expectedCount`
  - اگر Token retry شود و به join برسد، `arrivedKeys` شامل آن می‌شود
- **⚠️ مهم**: Incident **برای تصمیم‌گیری Join استفاده نمی‌شود** - فقط برای UX/Operations است

---

## 🔧 مسیر 2: Technical Failure (Technical Exception)

Technical Failure یک خطای تکنیکی/سیستمی است (مثلاً NullReferenceException، DatabaseException، ScriptException). این خطا باید به Incident تبدیل شود و قابل Retry/Manual resolve باشد.

### مرحله 1: Technical Exception در نود رخ می‌دهد

```
[ScriptTask: fraudCheck]
  ↓
  throw new InvalidOperationException("Amount too high!")
  ↓
[VariableMappingElementHandlerDecorator] catch
  ↓
  wrap → TokenExecutionException
  ↓
  throw (propagate به بالا)
```

**نکته مهم**: در این مرحله:
- ❌ Token هنوز Fail نشده است
- ❌ Incident ایجاد نشده است
- ✅ Transaction اول (Tx1) rollback می‌شود

### مرحله 2: Orchestrator Technical Exception را Catch می‌کند

```csharp
// در TokenProcessingOrchestrator.ProcessAsync()

try {
    await _uow.ExecuteInTransactionAsync(async trxCt => {
        // اجرای نود (Tx1)
        await _dispatcher.DispatchAsync(...);
    });
}
catch (TokenExecutionException tex) {
    // ✅ Technical Failure را catch می‌کنیم
    await HandleTechnicalFailureAsync(processId, tokenId, tex, ct);
}
catch (Exception ex) {
    // هر exception دیگری هم technical failure است
    await HandleTechnicalFailureAsync(
        processId,
        tokenId,
        new TokenExecutionException(processId, tokenId, "unknown", ex),
        ct);
}
```

**نکته مهم**: 
- Transaction اول (Tx1) **rollback شده است**
- هیچ تغییری در دیتابیس ذخیره نشده است
- Exception به یک transaction **جداگانه** (Tx2) منتقل می‌شود

### مرحله 3: HandleTechnicalFailureAsync (Tx2 - Transaction جداگانه)

```csharp
private async Task HandleTechnicalFailureAsync(...) {
    await _uow.ExecuteInTransactionAsync(async trxCt => {
        // 1. ایجاد Incident با ErrorType.TechnicalFailure و Stack Trace
        var stackTrace = tex.InnerException?.ToString() ?? tex.StackTrace ?? string.Empty;
        var incident = await _incidentService.CreateTechnicalFailureAsync(
            processId, tokenId, elementId, tex.Message, stackTrace, trxCt);
        
        // 2. Fail کردن Token با ErrorType.TechnicalFailure
        token.Fail(
            $"Technical failure: {tex.Message}",
            ErrorType.TechnicalFailure,
            errorCode: null,  // ❌ Technical Failure error code ندارد
            incident.Id);
        
        // 3. ذخیره تغییرات
        await _uow.SaveChangesAsync(trxCt);
    });
    
    // ✅ هیچ throw نمی‌کنیم - exception handle شده است
}
```

**نتیجه این مرحله**:
- ✅ **Incident ایجاد می‌شود** با `ErrorType.TechnicalFailure` و `Status = Open`
- ✅ **Stack Trace ذخیره می‌شود** در Incident
- ✅ **Token به `TokenState.Failed` تبدیل می‌شود** با `ErrorType.TechnicalFailure`
- ✅ **TokenFailedEvent** منتشر می‌شود
- ✅ همه چیز در دیتابیس **ذخیره می‌شود** (Tx2 commit می‌شود)

### مرحله 4-6: مشابه BPMN Error

TokenFailedEvent Handler، ProcessCompletionEvaluator و Join Gateway رفتار مشابهی دارند (همانند مسیر BPMN Error).

---

## 📊 مثال عملی: Parallel Gateway با BPMN Error

### سناریو: AND Split → دو شاخه موازی

```
[AND Split]
  ├─→ [validateAmount] ← BPMN Error! (INVALID_AMOUNT)
  └─→ [inventoryCheck] ← موفق
       ↓
    [AND Join] ← منتظر می‌ماند!
```

### جریان:

1. **validateAmount** BPMN Error می‌زند:
   - `throw new BpmnErrorException("INVALID_AMOUNT", "Amount exceeds limit")`
   - Token `validateAmount` → `Failed` با `ErrorType.BpmnError`
   - Incident ایجاد می‌شود (`ErrorType.BpmnError`, `Status = Open`, `ErrorCode = "INVALID_AMOUNT"`)
   - Token در همان نود (`validateAmount`) باقی می‌ماند

2. **inventoryCheck** موفق می‌شود:
   - Token `inventoryCheck` → `Active`
   - به `AND Join` می‌رسد

3. **AND Join** بررسی می‌کند:
   - `arrivedKeys.Count = 1` (فقط inventoryCheck با `ArrivedViaFlowId = "flowInventory"`)
   - `expectedCount = 2` (دو شاخه)
   - `arrivedKeys.Count (1) < expectedCount (2)` → ❌ **Join نمی‌شود** - منتظر می‌ماند
   - Join منتظر می‌ماند تا `validateAmount` retry شود و به join برسد با `ArrivedViaFlowId = "flowValidate"`

4. **پروسس**:
   - `liveTokens.Count = 2` (inventoryCheck: Active, validateAmount: Failed)
   - `openIncidents.Count = 1` (BPMN Error)
   - ❌ **پروسس تکمیل نمی‌شود** - `Running` باقی می‌ماند

---

## 📊 مثال عملی: Parallel Gateway با Technical Failure

### سناریو: AND Split → دو شاخه موازی

```
[AND Split]
  ├─→ [fraudCheck] ← Technical Exception! (NullReferenceException)
  └─→ [inventoryCheck] ← موفق
       ↓
    [AND Join] ← منتظر می‌ماند!
```

### جریان:

1. **fraudCheck** Technical Exception می‌زند:
   - `throw new NullReferenceException("Object reference not set")`
   - Wrap می‌شود به `TokenExecutionException`
   - Token `fraudCheck` → `Failed` با `ErrorType.TechnicalFailure`
   - Incident ایجاد می‌شود (`ErrorType.TechnicalFailure`, `Status = Open`, `StackTrace = "..."`)
   - Token در همان نود (`fraudCheck`) باقی می‌ماند

2. **inventoryCheck** موفق می‌شود:
   - Token `inventoryCheck` → `Active`
   - به `AND Join` می‌رسد

3. **AND Join** بررسی می‌کند:
   - `arrivedKeys.Count = 1` (فقط inventoryCheck با `ArrivedViaFlowId = "flowInventory"`)
   - `expectedCount = 2` (دو شاخه)
   - `arrivedKeys.Count (1) < expectedCount (2)` → ❌ **Join نمی‌شود** - منتظر می‌ماند
   - Join منتظر می‌ماند تا `fraudCheck` retry شود و به join برسد با `ArrivedViaFlowId = "flowFraud"`

4. **پروسس**:
   - `liveTokens.Count = 2` (inventoryCheck: Active, fraudCheck: Failed)
   - `openIncidents.Count = 1` (Technical Failure)
   - ❌ **پروسس تکمیل نمی‌شود** - `Running` باقی می‌ماند

---

## ✅ قوانین کلیدی

### قانون 1: Failed Token = Live Token
```csharp
private static bool IsLiveToken(Token token) {
    if (!token.IsExecutable) return false;
    
    return token.State is TokenState.Created
        or TokenState.Active
        or TokenState.Waiting
        or TokenState.Failed;  // ✅ Failed هم Live است (چه BPMN Error چه Technical Failure)
}
```

**چرا؟** چون Failed token هنوز می‌تواند retry شود یا resolve شود.

---

### قانون 2: Process Completion = Zero Live Tokens + Zero Open Incidents
```csharp
if (liveTokens.Count == 0 && openIncidents.Count == 0) {
    process.Complete();
}
```

**چرا؟** چون:
- اگر Open Incident وجود داشته باشد (چه BPMN Error چه Technical Failure)، یعنی هنوز مشکلی حل نشده است
- اگر Failed token وجود داشته باشد، یعنی هنوز کار ناتمام است

---

### قانون 3: Join فقط بر اساس Arrivals تصمیم‌گیری می‌کند
```csharp
// فقط tokens که به join رسیده‌اند (Waiting state)
var waiting = allTokens
    .Where(t =>
        t.CurrentElementId == arrivingToken.CurrentElementId &&
        t.State == TokenState.Waiting &&
        t.ScopeId == scopeId)
    .ToList();

// ArrivedViaFlowId: کلیدهای ورودی که tokens از طریق آن‌ها به join رسیده‌اند
var arrivedKeys = waiting
    .Select(t => t.ArrivedViaFlowId)
    .Where(x => !string.IsNullOrWhiteSpace(x))
    .Distinct()
    .ToList();

if (arrivedKeys.Count < expectedCount) {
    return true; // Still waiting
}
```

**چرا؟** چون:
- Join در BPMN 2.0 فقط منتظر tokens است که از incoming flows می‌آیند
- `ArrivedViaFlowId` دقیقاً نشان می‌دهد که کدام incoming flow token از آن آمده است
- اگر یک شاخه Failed شود و هنوز به join نرسیده باشد، `arrivedKeys` شامل آن نمی‌شود
- اگر Token retry شود و به join برسد، `arrivedKeys` شامل آن می‌شود
- **⚠️ مهم**: Incident برای تصمیم‌گیری Join استفاده نمی‌شود - فقط برای UX/Operations است

---

## 🔧 Retry و Resolution

### Retry یک Failed Token

```csharp
// در IncidentService
await _incidentService.RetryIncidentAsync(incidentId, ct);

// این کارها انجام می‌شود:
// 1. Incident.Retry() → Retries++
// 2. Token.State = Active (از Failed به Active)
// 3. TokenProcessingRequestedEvent منتشر می‌شود
// 4. Token دوباره اجرا می‌شود
```

**نکته**: Retry برای هر دو نوع Error (BPMN Error و Technical Failure) کار می‌کند.

### Resolve یک Incident

```csharp
// در IncidentService
await _incidentService.ResolveIncidentAsync(incidentId, ct);

// این کارها انجام می‌شود:
// 1. Incident.Resolve() → Status = Resolved
// 2. اگر همه Incidents resolve شوند و همه Tokens terminal باشند:
//    → Process.Complete()
```

**نکته**: Resolve برای هر دو نوع Error (BPMN Error و Technical Failure) کار می‌کند.

---

## 📝 خلاصه نهایی

| وضعیت | توکن | پروسس | Join | Incident |
|-------|------|--------|------|----------|
| **BPMN Error رخ می‌دهد** | Active → Failed (BpmnError) | Running | منتظر می‌ماند | Open (BpmnError, ErrorCode) |
| **Technical Exception رخ می‌دهد** | Active → Failed (TechnicalFailure) | Running | منتظر می‌ماند | Open (TechnicalFailure, StackTrace) |
| **بعد از Fail** | Failed (Live) | Running (تکمیل نمی‌شود) | منتظر می‌ماند | Open |
| **بعد از Retry** | Failed → Active | Running | منتظر می‌ماند | Open (Retries++) |
| **بعد از Resolve** | Failed (Terminal) | ممکن است Complete شود | ممکن است merge شود | Resolved |

---

## ⚠️ نکات مهم

1. **Transaction Rollback**: وقتی exception یا BPMN error رخ می‌دهد، Transaction اول (Tx1) rollback می‌شود. به همین دلیل `token.Fail()` در Decorator صدا زده نمی‌شود.

2. **Two-Phase Handling**: 
   - Phase 1 (Tx1): اجرای نود (اگر exception/error رخ دهد → rollback)
   - Phase 2 (Tx2): ثبت Incident و Fail کردن Token (همیشه commit می‌شود)

3. **Failed Token = Live Token**: یک Failed token (چه BPMN Error چه Technical Failure) هنوز "زنده" است و پروسس را زنده نگه می‌دارد.

4. **Join Behavior**: Join Gateway فقط بر اساس arrivals تصمیم‌گیری می‌کند:
   - منتظر می‌ماند تا `arrivedKeys.Count >= expectedCount`
   - ArrivedViaFlowId: کلیدهای ورودی که tokens از طریق آن‌ها به join رسیده‌اند
   - Incident برای تصمیم‌گیری Join استفاده نمی‌شود (فقط برای UX/Operations)

5. **Process Completion**: پروسس فقط وقتی Complete می‌شود که:
   - هیچ Live Token نباشد
   - هیچ Open Incident نباشد

6. **تفاوت BPMN Error و Technical Failure**:
   - **BPMN Error**: Error code دارد، Stack trace ندارد، باید توسط Error Boundary / Error EventSubprocess catch شود
   - **Technical Failure**: Error code ندارد، Stack trace دارد، به Incident تبدیل می‌شود و قابل Retry/Manual resolve است

---

## 🔄 TODO: BPMN Error Propagation

در حال حاضر، BPMN Error ها فقط Incident ایجاد می‌کنند و Token را Fail می‌کنند. در آینده باید:

1. **Error Boundary Detection**: پیدا کردن Error Boundary یا Error EventSubprocess که می‌تواند این Error Code را catch کند
2. **Token Propagation**: انتقال Token به Error Boundary / Error EventSubprocess
3. **Token Termination**: Terminate کردن Token فعلی و ایجاد Token جدید در Error Boundary

این قابلیت در حال پیاده‌سازی است.
