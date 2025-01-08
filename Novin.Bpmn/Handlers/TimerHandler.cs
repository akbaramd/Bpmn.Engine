// using Novin.Bpmn;
// using Novin.Bpmn.Models;
// using Quartz;
// using System;
// using System.Linq;
// using System.Threading.Tasks;
//
// public class TimerHandler : ITimerHandler
// {
//     private readonly ISchedulerFactory _schedulerFactory;
//
//     public TimerHandler(ISchedulerFactory schedulerFactory)
//     {
//         _schedulerFactory = schedulerFactory ?? throw new ArgumentNullException(nameof(schedulerFactory));
//     }
//
//     public async Task ExecuteAsync(BpmnBoundaryEvent boundaryEvent, BpmnProcessNode processNode, BpmnProcessExecutor processExecutor)
//     {
//         var timerEvents = boundaryEvent.Items.OfType<BpmnTimerEventDefinition>().ToList();
//
//         if (!timerEvents.Any())
//         {
//             Console.WriteLine($"No timer events found in boundary event {boundaryEvent.id} for node {processNode.ElementId}.");
//             return;
//         }
//
//         var scheduler = await _schedulerFactory.GetScheduler();
//
//         foreach (var timerEvent in timerEvents)
//         {
//             var isInterrupting = boundaryEvent.cancelActivity;
//             int duration = CalculateTimerDuration(timerEvent);
//
//             Console.WriteLine($"Scheduling {(isInterrupting ? "Interrupting" : "Non-Interrupting")} Timer for {duration}ms.");
//
//             var jobKey = new JobKey($"TimerJob-{processNode.Id}");
//             var triggerKey = new TriggerKey($"TimerTrigger-{processNode.Id}");
//
//             var job = JobBuilder.Create<TimerJob>()
//                 .WithIdentity(jobKey)
//                 .UsingJobData("ProcessId", processNode.Id.ToString())
//                 .UsingJobData("InstanceId", processExecutor.Instance.Id.ToString())
//                 .UsingJobData("IsInterrupting", isInterrupting)
//                 .Build();
//
//             job.JobDataMap["BoundaryEvent"] = boundaryEvent;
//
//             var trigger = TriggerBuilder.Create()
//                 .WithIdentity(triggerKey)
//                 .StartAt(DateTimeOffset.Now.AddMilliseconds(duration))
//                 .Build();
//
//             await scheduler.ScheduleJob(job, trigger);
//         }
//     }
//
//
//     public async Task CancelTimer(BpmnProcessNode processNode)
//     {
//         var scheduler = await _schedulerFactory.GetScheduler();
//         var triggerKey = new TriggerKey($"TimerTrigger-{processNode.Id}");
//         if (await scheduler.CheckExists(triggerKey))
//         {
//             await scheduler.UnscheduleJob(triggerKey);
//             Console.WriteLine($"Timer for ProcessNodeId {processNode.Id} has been canceled.");
//         }
//     }
//
//     private int CalculateTimerDuration(BpmnTimerEventDefinition timerEventDefinition)
//     {
//         if (!string.IsNullOrEmpty(timerEventDefinition.GetTimeDuration()) &&
//             int.TryParse(timerEventDefinition.GetTimeDuration(), out var duration))
//         {
//             return duration;
//         }
//
//         Console.WriteLine("No explicit timer duration defined. Using default value.");
//         return 5000; // Default 5 seconds
//     }
// }
//
// public class TimerJob : IJob
// {
//     private readonly BpmnEngine _bpmnEngine;
//
//     public TimerJob(BpmnEngine bpmnEngine)
//     {
//         _bpmnEngine = bpmnEngine ?? throw new ArgumentNullException(nameof(bpmnEngine));
//     }
//
//     public async Task Execute(IJobExecutionContext context)
//     {
//         var boundaryEvent = context.JobDetail.JobDataMap.GetValueOrDefault("BoundaryEvent") as BpmnBoundaryEvent;
//         var processNodeId = Guid.Parse(context.JobDetail.JobDataMap.GetString("ProcessId"));
//         var instanceId = Guid.Parse(context.JobDetail.JobDataMap.GetString("InstanceId"));
//         var isInterrupting = context.JobDetail.JobDataMap.GetBoolean("IsInterrupting");
//
//         Console.WriteLine($"Executing Timer Job for ProcessNodeId: {processNodeId}. IsInterrupting: {isInterrupting}");
//
//         try
//         {
//             var instance = await _bpmnEngine.GetProcessInstanceAsync(instanceId);
//             var processNode = instance.NodeStack.FirstOrDefault(x => x.Id == processNodeId);
//
//             if (processNode == null)
//             {
//                 Console.WriteLine($"ProcessNodeId {processNodeId} not found in instance {instanceId}.");
//                 return;
//             }
//
//             var executor = _bpmnEngine.GetExecutorForInstance(instance);
//
//             if (isInterrupting)
//             {
//                 // Handle interrupting boundary event
//                 processNode.Expire(); // Mark the activity node as non-executable
//                 var boundaryNode = executor.CreateNewNode(boundaryEvent, Guid.NewGuid(), true, processNode);
//                 await executor.FindNextNodes(boundaryNode);
//                 Console.WriteLine($"Interrupting Boundary Node {boundaryNode.ElementId} created and added to queue.");
//             }
//             else
//             {
//                 // Handle non-interrupting boundary event
//                 var boundaryNode = executor.CreateNewNode(boundaryEvent, Guid.NewGuid(), true, processNode);
//                 executor.EnqueueNext(boundaryNode);
//                 Console.WriteLine($"Non-Interrupting Boundary Node {boundaryNode.ElementId} created and added to queue.");
//             }
//
//             await executor.StartProcess();
//         }
//         catch (Exception ex)
//         {
//             Console.WriteLine($"Error while executing Timer Job for ProcessNodeId: {processNodeId}. Details: {ex.Message}");
//         }
//     }
// }
//
