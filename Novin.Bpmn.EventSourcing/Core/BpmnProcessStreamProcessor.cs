using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core.Models;
using Novin.Bpmn.EventSourcing.Events;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing.Core;

/// <summary>
/// پردازشگر جریان رویدادهای فرآیند BPMN
/// این پردازشگر با استفاده از معماری Event Sourcing، وضعیت فرآیند را بر اساس رویدادها بازسازی می‌کند
/// </summary>
public class BpmnProcessStreamProcessor : AbstractStreamProcessor
{
    private readonly IStateStore _stateStore;
    private readonly IUserTaskService _userTaskService;
    private readonly ILogger<BpmnProcessStreamProcessor> _logger;

    /// <summary>
    /// ایجاد یک نمونه جدید از پردازشگر فرآیند BPMN
    /// </summary>
    /// <param name="eventStore">مخزن رویدادها</param>
    /// <param name="eventBus">گذرگاه رویداد</param>
    /// <param name="stateStore">مخزن وضعیت</param>
    /// <param name="userTaskService">سرویس وظایف کاربری</param>
    /// <param name="logger">سیستم ثبت وقایع</param>
    public BpmnProcessStreamProcessor(
        IEventStore eventStore,
        IEventBus eventBus,
        IStateStore stateStore,
        IUserTaskService userTaskService,
        ILogger<BpmnProcessStreamProcessor> logger)
        : base("BpmnProcessStreamProcessor", eventStore, eventBus, logger)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _userTaskService = userTaskService ?? throw new ArgumentNullException(nameof(userTaskService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task HandleEventAsync(IBpmnEvent @event)
    {
        var processInstanceId = @event.ProcessInstanceId;
        
        // بازیابی وضعیت فعلی فرآیند (یا ایجاد یک وضعیت جدید)
        var (state, version) = await _stateStore.GetStateWithVersionAsync(processInstanceId);
        state ??= new BpmnProcessState
        {
            ProcessInstanceId = processInstanceId,
            Status = ProcessStatus.Created,
            Variables = new Dictionary<string, object>(),
            ActiveElements = new HashSet<string>(),
            CompletedElements = new HashSet<string>(),
        };


        // ذخیره وضعیت آپدیت شده
        await _stateStore.SaveStateAsync(processInstanceId, state, version > 0 ? version : null);
    }
}
