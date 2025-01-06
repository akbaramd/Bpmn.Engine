using Novin.Bpmn;
using Novin.Bpmn.Models;
using Quartz;


using Novin.Bpmn;
using Novin.Bpmn.Models;
using Quartz;
using System;
using System.Linq;
using System.Threading.Tasks;

public class TimerHandler : ITimerHandler
{
    private readonly ISchedulerFactory _schedulerFactory;

    public TimerHandler(ISchedulerFactory schedulerFactory)
    {
        _schedulerFactory = schedulerFactory ?? throw new ArgumentNullException(nameof(schedulerFactory));
    }

    public async Task ExecuteAsync(BpmnBoundaryEvent boundaryEvent, BpmnProcessNode processNode, BpmnProcessExecutor processExecutor)
    {
        var timerEvents = boundaryEvent.Items.OfType<BpmnTimerEventDefinition>().ToList();

        if (!timerEvents.Any())
        {
            Console.WriteLine($"No timer events found in boundary event {boundaryEvent.id} for node {processNode.ElementId}.");
            return;
        }

        var scheduler = await _schedulerFactory.GetScheduler();

        foreach (var timerEvent in timerEvents)
        {
            var isInterrupting = boundaryEvent.cancelActivity;
            int duration = CalculateTimerDuration(timerEvent);

            Console.WriteLine($"Scheduling {(isInterrupting ? "Interrupting" : "Non-Interrupting")} Timer for {duration}ms.");

            var jobKey = new JobKey($"TimerJob-{processNode.Id}");
            var triggerKey = new TriggerKey($"TimerTrigger-{processNode.Id}");

            var job = JobBuilder.Create<TimerJob>()
                .WithIdentity(jobKey)
                .UsingJobData("ProcessId", processNode.Id)
                .UsingJobData("InstanceId", processExecutor.Instance.Id.ToString())
                .UsingJobData("IsInterrupting", isInterrupting)
                .Build();

            job.JobDataMap["BoundaryEvent"] = boundaryEvent;

            var trigger = TriggerBuilder.Create()
                .WithIdentity(triggerKey)
                .StartAt(DateTimeOffset.Now.AddMilliseconds(duration))
                .Build();

          

            await scheduler.ScheduleJob(job, trigger);
        }
    }

    public  async Task CancelTimer( BpmnProcessNode processNode)
    {
        
        var scheduler = await _schedulerFactory.GetScheduler();
        var trigger = new TriggerKey($"TimerTrigger-{processNode.Id}");
        if (await scheduler.CheckExists(trigger))
        {
            await scheduler.UnscheduleJob(trigger);
        }
    
           
    }
    private int CalculateTimerDuration(BpmnTimerEventDefinition timerEventDefinition)
    {
        if (!string.IsNullOrEmpty(timerEventDefinition.GetTimeDuration()) &&
            int.TryParse(timerEventDefinition.GetTimeDuration(), out var duration))
        {
            return duration;
        }

        Console.WriteLine("No explicit timer duration defined. Using default value.");
        return 5000; // Default 5 seconds
    }
}



public class TimerJob : IJob
{
    private readonly BpmnEngine _bpmnEngine;

    public TimerJob(BpmnEngine bpmnEngine)
    {
        _bpmnEngine = bpmnEngine ?? throw new ArgumentNullException(nameof(bpmnEngine));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var boundaryEvent = context.JobDetail.JobDataMap.GetValueOrDefault("BoundaryEvent") as BpmnBoundaryEvent;
        var processNodeId = context.JobDetail.JobDataMap.GetGuidValue("ProcessId");
        var instanceId = context.JobDetail.JobDataMap.GetGuidValue("InstanceId");
        var isInterrupting = context.JobDetail.JobDataMap.GetBooleanValue("IsInterrupting");

        Console.WriteLine($"Executing Timer Job for ProcessNodeId: {processNodeId}. IsInterrupting: {isInterrupting}");

        try
        {
            // Reload the process instance using BpmnEngine
            var instance = await _bpmnEngine.GetProcessInstanceAsync(instanceId);
            var processNode = instance.NodeStack.FirstOrDefault(x => x.Id == processNodeId);

            if (processNode != null && (!processNode.UserTask?.IsCompleted ?? true))
            {
                if (isInterrupting)
                {
                    processNode.Expire();
                    Console.WriteLine($"Interrupting Timer expired for ProcessNodeId: {processNodeId}. Resuming execution...");
                }
                else
                {
                    Console.WriteLine($"Non-Interrupting Timer expired for ProcessNodeId: {processNodeId}. Resuming execution...");
                }

                // Get the executor for the process instance
                var executor = _bpmnEngine.GetExecutor(instance);

              
                var newNode = executor.CreateNewNode(boundaryEvent, Guid.NewGuid(), true, processNode);
                Console.WriteLine($"Created new node {newNode.ElementId} from ProcessNodeId: {processNodeId}.");

                await executor.FindNextNodes(newNode);
                await executor.StartProcess();
            }
            else
            {
                Console.WriteLine($"ProcessNodeId {processNodeId} has already completed its activity.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while executing Timer Job for ProcessNodeId: {processNodeId}. Details: {ex.Message}");
        }
    }
}

