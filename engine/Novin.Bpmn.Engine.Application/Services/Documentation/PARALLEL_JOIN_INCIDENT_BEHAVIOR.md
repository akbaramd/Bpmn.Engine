# Parallel Join Behavior (BPMN 2.0 Semantics)

## قانون کلی (BPMN 2.0 Compliant)

**Join فقط بر اساس arrivals تصمیم‌گیری می‌کند:**
1. همه شاخه‌های مورد انتظار به join رسیده باشند (`arrivedKeys.Count >= expectedCount`)
2. ArrivedViaFlowId: کلیدهای ورودی که tokens از طریق آن‌ها به join رسیده‌اند

**⚠️ مهم**: Join **به Incident وابسته نیست**. Incident فقط برای UX/Operations است (نمایش، retry، manual ops).

## سناریو: یک شاخه Fail شده

### وضعیت:
- **شاخه A**: Fail → `TokenA = Failed` + `Incident(Open)` (هنوز به join نرسیده)
- **شاخه B**: می‌رسد به join → `TokenB = Waiting` (در join) با `ArrivedViaFlowId = "flowB"`

### رفتار BPMN:
- Join **منتظر می‌ماند** تا `arrivedKeys.Count >= expectedCount`
- اگر `expectedCount = 2` و فقط `flowB` رسیده باشد (`arrivedKeys.Count = 1`):
  - Join **merge نمی‌شود** (هنوز منتظر `flowA` است)
- اگر `TokenA` retry شود و به join برسد با `ArrivedViaFlowId = "flowA"`:
  - `arrivedKeys` شامل `["flowA", "flowB"]` می‌شود
  - `arrivedKeys.Count = 2 >= expectedCount` → Join **merge می‌شود**

### پیاده‌سازی:
```csharp
// در GatewayJoinService.TryJoinAsync:

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

// تصمیم‌گیری فقط بر اساس arrivals
if (arrivedKeys.Count < expectedCount)
{
    return true; // Still waiting
}

// Merge: همه شاخه‌های مورد انتظار رسیده‌اند
```

## نکته مهم درباره ExpectedCount

**قانون**: `expectedCount` در زمان Split ثبت می‌شود و نشان‌دهنده تعداد شاخه‌های واقعی است.

**دلیل**: 
- اگر یک شاخه Fail شود و هنوز به join نرسیده باشد، Join منتظر می‌ماند
- اگر شاخه retry شود و به join برسد، `arrivedKeys` شامل آن می‌شود
- اگر شاخه terminate شود و هرگز به join نرسد، `arrivedKeys` شامل آن نمی‌شود
- **Join فقط بر اساس tokens که واقعاً به join رسیده‌اند تصمیم‌گیری می‌کند**

## Completion Rule

پروسس فقط وقتی `Completed` می‌شود که:
1. هیچ توکن Live نباشد (`Active`/`Waiting`/`Failed`)
2. هیچ Incident باز نباشد (`Open`)
3. `ProcessState` هم `Running` باشد

**نتیجه**: اگر حتی یک Failed token با Open Incident وجود دارد، پروسس تمام نشده.

