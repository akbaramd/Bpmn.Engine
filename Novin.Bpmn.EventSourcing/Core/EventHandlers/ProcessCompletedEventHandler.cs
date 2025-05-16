using Novin.Bpmn.EventSourcing.Core.Executions;
using Novin.Bpmn.EventSourcing.Events;
using System;
using System.Threading;
using System.Threading.Tasks;

public class ProcessCompletedEventHandler : BpmnEventHandlerBase<ProcessCompleted>
{
    private readonly IExecutionContextRepository _contextRepository;

    public ProcessCompletedEventHandler(IServiceProvider serviceProvider,
        IExecutionContextRepository contextRepository)
        : base(serviceProvider)
    {
        _contextRepository = contextRepository ?? throw new ArgumentNullException(nameof(contextRepository));
    }

    public override async Task HandleAsync(ProcessCompleted @event, CancellationToken cancellationToken = default)
    {
        // 1. می‌توان کانتکست‌های مرتبط با Instance را پاک یا به حالت نهایی ببریم
        var contexts = _contextRepository.GetByInstanceId(@event.InstanceId);

        foreach (var ctx in contexts)
        {
            // اگر نیاز به پاکسازی است، اینجا حذف کنیم
            _contextRepository.Remove(ctx.ContextId);
        }

        // 2. منطق اضافه می‌تواند اینجا قرار گیرد (مانند نوتیفیکیشن، ثبت لاگ و ...)

        await Task.CompletedTask;
    }
}