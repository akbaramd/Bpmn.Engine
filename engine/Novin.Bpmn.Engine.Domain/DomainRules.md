# Domain Rules: Error Handling & Process Completion

## قانون 1: Failed Token = Live Token (Incident-Driven Execution)

**قاعده**: یک `Failed` token هنوز "زنده" (live) است و پروسس نباید complete شود تا زمانی که:
- Failed token retry شود و موفق شود
- Failed token manual resolve شود (via Move/Terminate)
- Failed token terminate شود (explicit termination)

**دلیل**: 
- Failed token ممکن است قابل retry باشد
- Failed token ممکن است نیاز به manual intervention داشته باشد
- پروسس نباید complete شود در حالی که یک token در حالت failed است
- این منطق برای **Incident-Driven Execution** کاملاً درست و رایج است

**⚠️ نکته مهم**: از نظر BPMN 2.0، "Failed" state استاندارد نیست - این یک **state موتوری** است که برای مدیریت incidents استفاده می‌شود.

**پیاده‌سازی**:
- `ProcessCompletionEvaluator.IsLiveToken()` باید `TokenState.Failed` را به عنوان "زنده" حساب کند
- Process فقط وقتی complete می‌شود که هیچ live token (شامل Failed) باقی نمانده باشد

## قانون 2: انواع خطا و نحوه Handle شدن

### BPMN Error (ThrowBpmnError)
- خطای business که مدل می‌خواهد آن را با Error Boundary / Error EventSubprocess بگیرد
- این «خرابی سیستم» نیست
- باید توسط مدل BPMN handle شود
- **جریان Handle شدن**:
  1. Error باید **propagate** شود تا Error Boundary / Error EventSubprocess پیدا شود
  2. اگر Error Boundary پیدا شد: Token به Error Boundary منتقل می‌شود
  3. اگر Error Boundary پیدا نشد: Token **terminate** می‌شود (Unhandled BPMN Error)
  4. **⚠️ مهم**: BPMN Error که handler ندارد، **Incident ایجاد نمی‌کند** - فقط Token terminate می‌شود

### Technical Failure (ThrowTechnicalFailure)
- خطای تکنیکی/سیستمی (اسکریپت، DB، HTTP، NullRef، ...)
- باید به **Incident** تبدیل شود
- Token به `Failed` تبدیل می‌شود (نه Terminate)
- قابل Retry/Manual resolve است
- بقیه شاخه‌ها می‌توانند ادامه دهند (Incident-Driven Execution)

## قانون 3: Incident Lifecycle

1. **ایجاد**: فقط وقتی یک **Technical Failure** رخ می‌دهد
   - ⚠️ **BPMN Error که handler ندارد، Incident ایجاد نمی‌کند** - فقط Token terminate می‌شود
2. **Retry**: می‌تواند retry شود (افزایش `Retries`)
3. **Resolve**: می‌تواند manual resolve شود
4. **Reopen**: می‌تواند بعد از resolve دوباره باز شود

## قانون 4: Process Completion

Process فقط وقتی complete می‌شود که:
- هیچ live token (Created/Active/Waiting/Failed) باقی نمانده باشد
- همه tokens در حالت terminal (Completed/Terminated) باشند
- هیچ Open Incident وجود نداشته باشد

**نکته**: Failed token = live token → Process complete نمی‌شود

---

## قانون 5: Process Derived Status

از آنجایی که `ProcessState=Running` ممکن است گمراه‌کننده باشد وقتی که Open Incidents وجود دارد،
یک **Derived Status** برای نمایش وضعیت دقیق‌تر Process استفاده می‌شود:

- **Running**: Process در حال اجرا است بدون هیچ Incident
- **RunningWithIncidents**: Process در حال اجرا است اما Open Incidents دارد (Incident-Driven Execution - Blocked but recoverable)
- **Suspended**: Process به صورت دستی suspend شده است
- **Completed**: Process با موفقیت تمام شده است
- **Terminated**: Process به صورت دستی terminate شده است
- **Failed**: Process fail شده است (Fail-fast policy - غیرقابل بازیابی)

**استفاده**: از `IProcessStatusService.GetDerivedStatusAsync()` برای دریافت Derived Status استفاده کنید.
این سرویس بر اساس ProcessState، وجود Open Incidents، و Failed Tokens محاسبه می‌شود.

