using Novin.Bpmn.EventSourcing.Core.Executions;
using Novin.Bpmn.EventSourcing.Events;
using System;
using System.Threading;
using System.Threading.Tasks;
using ExecutionContext = Novin.Bpmn.EventSourcing.Core.Executions.ExecutionContext;

public class ElementCreatedEventHandler : BpmnEventHandlerBase<ElementCreated>
{
    private readonly IExecutionContextRepository _contextRepository;
    private readonly IJoinResolverService _joinResolverService;
    private readonly IFlowTopologyStore _topologyStore;
    // فرض کنیم AppendEvent در کلاس پایه یا جایی در دسترس است

    public ElementCreatedEventHandler(IServiceProvider serviceProvider,
                                      IExecutionContextRepository contextRepository,
                                      IJoinResolverService joinResolverService,
                                      IFlowTopologyStore topologyStore)
        : base(serviceProvider)
    {
        _contextRepository = contextRepository ?? throw new ArgumentNullException(nameof(contextRepository));
        _joinResolverService = joinResolverService ?? throw new ArgumentNullException(nameof(joinResolverService));
        _topologyStore = topologyStore ?? throw new ArgumentNullException(nameof(topologyStore));
    }

    public override async Task HandleAsync(ElementCreated @event, CancellationToken cancellationToken = default)
    {
     



        // 1. سعی در گرفتن ExecutionContext موجود
        var existingContext = _contextRepository.Get(@event.ExecutionId);

        // 2. بارگذاری توپولوژی
        var topology = _topologyStore.Get(@event.DeploymentId, @event.ProcessId);
        if (topology == null)
            throw new InvalidOperationException("Topology not found");

        bool isJoinNode = topology.Nodes.TryGetValue(@event.ElementId.ToString(), out var node) && node.IsJoinNode;

        if (isJoinNode)
        {
            // گرفتن شاخه‌های ورودی برای Join
            var incomingBranches = topology.Incoming.TryGetValue(@event.ElementId.ToString(), out var incomingIds)
                ? incomingIds
                : new List<string>();

            // گرفتن کانتکست‌های شاخه‌های ورودی که State == Completed و InstanceId برابر
            var contextsToMerge = new List<ExecutionContext>();

            foreach (var branchElementIdStr in incomingBranches)
            {
                if (!Guid.TryParse(branchElementIdStr, out var branchElementId))
                    continue;

                var branchContexts = _contextRepository.GetByInstanceId(@event.InstanceId)
                    .Where(c => Guid.TryParse(c.CurrentElementId, out var cElementId) && cElementId == branchElementId && c.State == ExecutionState.Completed)
                    .ToList();

                contextsToMerge.AddRange(branchContexts);
            }

            // بررسی امکان Join
            if (!_joinResolverService.CanJoin(topology, @event.ElementId.ToString(), contextsToMerge))
            {
                // همه شاخه‌ها کامل نشده‌اند
                // این رویداد را مجددا به صف اضافه کنید یا مکانیزمی برای Retry قرار دهید
                AppendEvent(@event);
                return;
            }

            // ادغام کانتکست‌ها
            var mergedContext = _joinResolverService.MergeContexts(topology, @event.ElementId.ToString(), contextsToMerge);

            // حذف کانتکست‌های قدیمی
            foreach (var ctx in contextsToMerge)
                _contextRepository.Remove(ctx.ContextId);

            // ذخیره کانتکست ادغام شده
            _contextRepository.Save(mergedContext);

            // انتشار رویداد پردازش شروع برای المان Join
            await PublishElementProcessingEvent(@event, mergedContext.ContextId);
        }
        else
        {
            if (existingContext != null)
            {
                // کانتکست موجود است؛ آن را بروزرسانی می‌کنیم
                existingContext.CurrentElementId = @event.ElementId.ToString();
                existingContext.State = ExecutionState.Active;
                existingContext.Version++;

                _contextRepository.Save(existingContext);

                // انتشار رویداد پردازش برای ادامه کار
                await PublishElementProcessingEvent(@event, existingContext.ContextId);
            }
           
        }

        await Task.CompletedTask;
    }

    // متد انتشار رویداد پردازش المان (TaskProcessing یا موارد تخصصی‌تر)
    private async Task PublishElementProcessingEvent(ElementCreated @event, Guid contextId)
    {
        var processingEvent = new ElementProcessing
        {
            EventId = Guid.NewGuid(),
            InstanceId = @event.InstanceId,
            DeploymentId = @event.DeploymentId,
            DeploymentKey = @event.DeploymentKey,
            ProcessId = @event.ProcessId,
            ElementId = @event.ElementId,
            ExecutionId = contextId,
            Timestamp = DateTime.UtcNow,
            ElementType = @event.ElementType,
            Version = 1,
            IsExecutable = true
        };

        // فرض کنیم AppendEvent یک متد async برای انتشار رویداد است
         AppendEvent(processingEvent);
    }

   
}
