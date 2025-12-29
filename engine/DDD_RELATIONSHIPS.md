# DDD Relationships and Aggregate Design

این سند روابط بین Aggregate Root ها و طراحی آن‌ها بر اساس اصول DDD را توضیح می‌دهد.

## Aggregate Roots

### 1. Deployment
**مسئولیت**: مدیریت تعاریف فرآیند BPMN
- نگهداری BPMN XML
- مدیریت نسخه‌ها
- فعال/غیرفعال کردن deployment

**روابط**:
- هیچ رابطه مستقیمی با سایر Aggregate ها ندارد
- Process از DeploymentKey برای ارجاع استفاده می‌کند

### 2. Process
**مسئولیت**: مدیریت نمونه فرآیند (Process Instance)
- مدیریت چرخه حیات فرآیند
- نگهداری متغیرهای فرآیند
- ردیابی Node ها و Token های مرتبط (فقط ID)

**روابط** (بر اساس DDD - فقط ID):
- `NodeIds`: لیست ID های Node های مرتبط
- `TokenIds`: لیست ID های Token های مرتبط
- **نکته**: Process مستقیماً Node یا Token را نگه نمی‌دارد، فقط ID ها را ردیابی می‌کند

### 3. Node
**مسئولیت**: مدیریت نودهای BPMN (Task, Gateway, Event)
- نگهداری متغیرها (Variables)
- ردیابی Token های فعلی در نود
- تاریخچه Token هایی که از نود عبور کرده‌اند

**روابط**:
- `ProcessId`: ارجاع به Process (فقط ID)
- `CurrentTokenIds`: لیست ID های Token های فعلی در این نود
- `TokenHistory`: تاریخچه Token هایی که از نود عبور کرده‌اند

**ویژگی‌های مهم**:
- ✅ **دارای Variables است** (InputVariables و OutputVariables حذف شدند، فقط Variables)
- ✅ **ردیابی Token های فعلی**: `CurrentTokenIds`
- ✅ **تاریخچه Token ها**: `TokenHistory` (TokenHistoryEntry)

### 4. Token
**مسئولیت**: مدیریت توکن‌های اجرایی
- ردیابی موقعیت فعلی (CurrentElementId, CurrentNodeId)
- تاریخچه نودهایی که از آن‌ها عبور کرده
- پشتیبانی از Fork (ParentTokenId)

**روابط**:
- `ProcessId`: ارجاع به Process (فقط ID)
- `CurrentNodeId`: ارجاع به Node فعلی (فقط ID)
- `ParentTokenId`: ارجاع به Token والد (برای Fork)

**ویژگی‌های مهم**:
- ❌ **بدون Variables**: Token متغیر ندارد (متغیرها در Node نگه‌داری می‌شوند)
- ✅ **تاریخچه نودها**: `NodeHistory` - لیست نودهایی که Token از آن‌ها عبور کرده
- ✅ **موقعیت فعلی**: `CurrentElementId` و `CurrentNodeId`

### 5. Task
**مسئولیت**: مدیریت Task های BPMN (UserTask, ServiceTask, etc.)
- مدیریت وضعیت Task
- نگهداری Input/Output Variables
- مدیریت تخصیص (Assignment)

**روابط**:
- `ProcessId`: ارجاع به Process (فقط ID)

## اصول DDD اعمال شده

### 1. Aggregate Boundaries
- هر Aggregate Root مستقل است و فقط ID سایر Aggregate ها را نگه می‌دارد
- هیچ Aggregate مستقیماً Aggregate دیگر را نگه نمی‌دارد

### 2. Consistency Boundaries
- هر Aggregate مسئول حفظ انسجام خود است
- تغییرات در یک Aggregate نباید مستقیماً Aggregate دیگر را تغییر دهد

### 3. References Between Aggregates
- فقط از ID استفاده می‌شود (نه Reference مستقیم)
- Process → Node: فقط `NodeIds`
- Process → Token: فقط `TokenIds`
- Node → Token: فقط `CurrentTokenIds` و `TokenHistory`
- Token → Node: فقط `CurrentNodeId`

### 4. Domain Events
- هر تغییر مهم در Aggregate یک Domain Event منتشر می‌کند
- Event ها برای هماهنگی بین Aggregate ها استفاده می‌شوند

## مثال جریان کار

```
1. Process ایجاد می‌شود
   → ProcessInstanceCreatedEvent منتشر می‌شود

2. Node ایجاد می‌شود
   → Process.AddNodeId(nodeId) فراخوانی می‌شود
   → NodeCreatedEvent منتشر می‌شود

3. Token ایجاد می‌شود
   → Process.AddTokenId(tokenId) فراخوانی می‌شود
   → TokenCreatedEvent منتشر می‌شود

4. Token وارد Node می‌شود
   → Node.AddTokenToNode(tokenId) فراخوانی می‌شود
   → Token.EnterNode(nodeId, elementId) فراخوانی می‌شود
   → Node.StartProcessing(tokenId) فراخوانی می‌شود

5. Node پردازش می‌شود
   → Node.Variables به‌روزرسانی می‌شود
   → NodeProcessingEvent منتشر می‌شود

6. Node تکمیل می‌شود
   → Node.Complete(tokenId, outputVariables) فراخوانی می‌شود
   → Token از CurrentTokenIds حذف می‌شود و به TokenHistory اضافه می‌شود
   → Token.LeaveNode() فراخوانی می‌شود
   → NodeCompletedEvent منتشر می‌شود

7. Token به Node بعدی حرکت می‌کند
   → Token.MoveToNextStep(nextElementId, nextNodeId) فراخوانی می‌شود
   → TokenMovedEvent منتشر می‌شود
```

## نکات مهم

1. **Variables در Node**: تمام متغیرهای مربوط به یک نود در `Node.Variables` نگه‌داری می‌شوند
2. **Token بدون Variables**: Token فقط موقعیت و تاریخچه را نگه می‌دارد
3. **تاریخچه دو طرفه**: 
   - Node تاریخچه Token هایی که از آن عبور کرده را دارد
   - Token تاریخچه نودهایی که از آن‌ها عبور کرده را دارد
4. **Current Tokens**: Node می‌داند کدام Token ها در حال حاضر در آن هستند
5. **References فقط با ID**: تمام روابط بین Aggregate ها فقط با ID است

