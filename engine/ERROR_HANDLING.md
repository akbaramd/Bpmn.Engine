# Error Handling در BPMN Engine

این سند توضیح می‌دهد که چگونه error handling در این پروژه انجام می‌شود.

## 🔴 مشکل فعلی: Error Handling ناقص است

در حال حاضر، اگر یک نود در اجرای پروسس خطا داشته باشد:

### ✅ چیزهایی که انجام می‌شود:
1. **Token Fail می‌شود**: در `VariableMappingElementHandlerDecorator` اگر exception رخ دهد، token با `token.Fail()` fail می‌شود
2. **Transaction Rollback می‌شود**: در `ExecuteInTransactionAsync` اگر exception رخ دهد، transaction rollback می‌شود
3. **Exception Log می‌شود**: در decorator و orchestrator exception log می‌شود

### ❌ چیزهایی که انجام نمی‌شود:
1. **Exception به بالا propagate می‌شود**: هیچ global error handler وجود ندارد
2. **Process Fail نمی‌شود**: اگر token fail شود، process خودکار fail نمی‌شود (فقط evaluation می‌شود)
3. **Error Boundary وجود ندارد**: هیچ mechanism برای catch کردن exception در سطح بالاتر وجود ندارد

## 📋 جریان فعلی Error Handling

### 1. سطح Decorator (`VariableMappingElementHandlerDecorator`)

```csharp
try
{
    // Phase 0: Reset Token Locals
    // Phase 1: ApplyInputs
    // Phase 2: Execute Business Logic (via inner handler)
    // Phase 3: ApplyOutputs
}
catch (Exception ex)
{
    // ✅ Token را fail می‌کند
    if (token.State is not TokenState.Failed and not TokenState.Terminated)
    {
        token.Fail($"Mapping decorator error: {ex.Message}");
    }
    
    // ❌ Exception را re-throw می‌کند
    throw; // re-throw برای error handling بالاتر
}
```

**نتیجه**: Token fail می‌شود، اما exception به بالا propagate می‌شود.

### 2. سطح Dispatcher (`TokenExecutionDispatcher`)

```csharp
public Task DispatchAsync(...)
{
    // اگر handler پیدا نشود:
    if (matches.Count == 0)
    {
        token.Fail(error);
        return Task.CompletedTask; // ✅ Exception نمی‌دهد
    }
    
    // اگر handler exception بدهد:
    return handler.HandleAsync(...); // ❌ Exception propagate می‌شود
}
```

**نتیجه**: اگر handler پیدا نشود، token fail می‌شود. اما اگر handler exception بدهد، exception propagate می‌شود.

### 3. سطح Orchestrator (`TokenProcessingOrchestrator`)

```csharp
public Task ProcessAsync(...)
    => _uow.ExecuteInTransactionAsync(async trxCt =>
    {
        // ❌ هیچ try-catch ندارد
        await _dispatcher.DispatchAsync(process, token, element, ctx, trxCt);
    }, ct);
```

**نتیجه**: هیچ try-catch ندارد. Exception به `ExecuteInTransactionAsync` propagate می‌شود.

### 4. سطح Transaction (`ExecuteInTransactionAsync`)

```csharp
try
{
    await action(ct);
    await CommitTransactionAsync(ct);
}
catch (Exception ex)
{
    _logger.LogError(ex, "ExecuteInTransactionAsync failed. Rolling back.");
    await RollbackTransactionAsync(ct);
    
    // ❌ Exception را re-throw می‌کند
    throw;
}
```

**نتیجه**: Transaction rollback می‌شود، اما exception دوباره throw می‌شود.

### 5. سطح Event Handler (`TokenProcessingRequestedEventHandler`)

```csharp
public Task Handle(TokenProcessingRequestedEvent n, CancellationToken ct)
    => _orchestrator.ProcessAsync(n.ProcessId, n.TokenId, ct);
```

**نتیجه**: هیچ try-catch ندارد. Exception به MediatR propagate می‌شود.

## 🎯 سناریوهای مختلف

### سناریو 1: ScriptTask Exception

1. `ScriptTaskHandler` → `IScriptTaskExecutor.ExecuteAsync()`
2. Script exception می‌دهد
3. `IScriptTaskExecutor` → `token.Fail()` صدا می‌زند
4. Exception به `VariableMappingElementHandlerDecorator` propagate می‌شود
5. Decorator → `token.Fail()` دوباره صدا می‌زند (اگر قبلاً fail نشده باشد)
6. Exception به `TokenExecutionDispatcher` propagate می‌شود
7. Exception به `TokenProcessingOrchestrator` propagate می‌شود
8. `ExecuteInTransactionAsync` → Transaction rollback می‌شود
9. Exception به `TokenProcessingRequestedEventHandler` propagate می‌شود
10. **❌ هیچ error handler وجود ندارد → Exception به MediatR propagate می‌شود**

### سناریو 2: Gateway Exception

1. `GatewayHandler` → `GatewaySplitService.TrySplitAsync()`
2. Gateway exception می‌دهد (مثلاً `process.AddToken()` خطا می‌دهد)
3. Exception به `VariableMappingElementHandlerDecorator` propagate می‌شود
4. Decorator → `token.Fail()` صدا می‌زند
5. Exception به بالا propagate می‌شود
6. Transaction rollback می‌شود
7. **❌ هیچ error handler وجود ندارد**

### سناریو 3: Variable Mapping Exception

1. `VariableMappingElementHandlerDecorator` → `ApplyInputs()` یا `ApplyOutputs()`
2. Mapping exception می‌دهد
3. Decorator → `token.Fail()` صدا می‌زند
4. Exception به بالا propagate می‌شود
5. Transaction rollback می‌شود
6. **❌ هیچ error handler وجود ندارد**

## ⚠️ مشکلات فعلی

1. **Exception به بالا propagate می‌شود**: هیچ global error handler وجود ندارد
2. **Transaction Rollback می‌شود**: اگر exception رخ دهد، همه تغییرات rollback می‌شوند (حتی token.Fail())
3. **Process Fail نمی‌شود**: اگر token fail شود، process خودکار fail نمی‌شود
4. **No Error Recovery**: هیچ mechanism برای retry یا recovery وجود ندارد

## 💡 پیشنهادات برای بهبود

### 1. اضافه کردن Global Error Handler

```csharp
public sealed class TokenProcessingRequestedEventHandler
    : INotificationHandler<TokenProcessingRequestedEvent>
{
    public async Task Handle(TokenProcessingRequestedEvent n, CancellationToken ct)
    {
        try
        {
            await _orchestrator.ProcessAsync(n.ProcessId, n.TokenId, ct);
        }
        catch (Exception ex)
        {
            // Handle exception gracefully
            // - Log error
            // - Mark token as failed (if not already failed)
            // - Evaluate process completion
            // - Don't re-throw (prevent exception propagation)
        }
    }
}
```

### 2. بهبود Decorator Error Handling

```csharp
catch (Exception ex)
{
    // اگر token هنوز fail نشده، fail می‌کنیم
    if (token.State is not TokenState.Failed and not TokenState.Terminated)
    {
        token.Fail($"Mapping decorator error: {ex.Message}");
    }
    
    // ❌ Exception را re-throw نکنیم - بگذاریم token fail شود و ادامه ندهیم
    // throw; // حذف شود
    return; // به جای throw
}
```

### 3. اضافه کردن Process Failure Policy

```csharp
public sealed class TokenFailedEventHandler : INotificationHandler<TokenFailedEvent>
{
    public async Task Handle(TokenFailedEvent notification, CancellationToken cancellationToken)
    {
        // Policy: Fail = Process Failed + terminate all other tokens
        var process = await _uow.Processes.GetByIdAsync(notification.ProcessId, cancellationToken);
        if (process != null && process.State == ProcessState.Running)
        {
            // Terminate all other live tokens
            var allTokens = await _uow.Tokens.GetByProcessIdAsync(notification.ProcessId, cancellationToken);
            foreach (var token in allTokens.Where(t => t.Id != notification.TokenId && IsLiveToken(t)))
            {
                token.Terminate($"Process failed due to token {notification.TokenId} failure");
            }
            
            // Fail the process
            process.Fail($"Process failed due to token failure: {notification.Error}");
        }
        
        // Evaluate completion (will see process is Failed)
        await _evaluator.EvaluateCompletionAsync(notification.ProcessId, cancellationToken);
    }
}
```

## 📝 خلاصه

**وضعیت فعلی**:
- ✅ Token fail می‌شود
- ✅ Transaction rollback می‌شود
- ✅ Exception log می‌شود
- ❌ Exception به بالا propagate می‌شود
- ❌ Process خودکار fail نمی‌شود
- ❌ هیچ error recovery وجود ندارد

**پیشنهاد**: اضافه کردن global error handler و بهبود error handling در decorator و event handlers.

