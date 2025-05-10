using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core;
using Novin.Bpmn.EventSourcing.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing.Examples;

/// <summary>
/// Example of using BPMN User Tasks with Event Sourcing
/// </summary>
public class UserTaskExample
{
    // Updated BPMN process with multiple user tasks and boundary events
    private const string UserTaskFlowXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<definitions xmlns=""http://www.omg.org/spec/BPMN/20100524/MODEL"" 
             xmlns:bpmndi=""http://www.omg.org/spec/BPMN/20100524/DI"" 
             xmlns:dc=""http://www.omg.org/spec/DD/20100524/DC"" 
             xmlns:di=""http://www.omg.org/spec/DD/20100524/DI"" 
             id=""Definitions_1"" 
             targetNamespace=""http://bpmn.io/schema/bpmn"">
  <process id=""Process_1"" isExecutable=""true"">
    <startEvent id=""StartEvent_1"" name=""Start"">
      <outgoing>Flow_1</outgoing>
    </startEvent>
    <sequenceFlow id=""Flow_1"" sourceRef=""StartEvent_1"" targetRef=""UserTask_1"" />
    
    <!-- First user task with a non-interrupting timer boundary event -->
    <userTask id=""UserTask_1"" name=""Initial Request Review"">
      <incoming>Flow_1</incoming>
      <outgoing>Flow_2</outgoing>
    </userTask>
    <boundaryEvent id=""TimerBoundary_1"" name=""Reminder Timer"" attachedToRef=""UserTask_1"" cancelActivity=""false"">
      <outgoing>Flow_Reminder</outgoing>
      <timerEventDefinition>
        <timeDuration>PT10S</timeDuration>
      </timerEventDefinition>
    </boundaryEvent>
    <sequenceFlow id=""Flow_Reminder"" sourceRef=""TimerBoundary_1"" targetRef=""ServiceTask_Reminder"" />
    <serviceTask id=""ServiceTask_Reminder"" name=""Send Reminder"">
      <incoming>Flow_Reminder</incoming>
      <outgoing>Flow_ReminderEnd</outgoing>
    </serviceTask>
    <sequenceFlow id=""Flow_ReminderEnd"" sourceRef=""ServiceTask_Reminder"" targetRef=""EndEvent_Reminder"" />
    <endEvent id=""EndEvent_Reminder"" name=""Reminder Sent"">
      <incoming>Flow_ReminderEnd</incoming>
    </endEvent>
    
    <sequenceFlow id=""Flow_2"" sourceRef=""UserTask_1"" targetRef=""UserTask_2"" />
    
    <!-- Second user task with an interrupting message boundary event -->
    <userTask id=""UserTask_2"" name=""Manager Approval"">
      <incoming>Flow_2</incoming>
      <outgoing>Flow_3</outgoing>
    </userTask>
    <boundaryEvent id=""MessageBoundary_1"" name=""Urgent Cancellation"" attachedToRef=""UserTask_2"" cancelActivity=""true"">
      <outgoing>Flow_Cancel</outgoing>
      <messageEventDefinition messageRef=""Message_Urgent_Cancel"" />
    </boundaryEvent>
    <sequenceFlow id=""Flow_Cancel"" sourceRef=""MessageBoundary_1"" targetRef=""ServiceTask_Cancel"" />
    <serviceTask id=""ServiceTask_Cancel"" name=""Process Cancellation"">
      <incoming>Flow_Cancel</incoming>
      <outgoing>Flow_CancelEnd</outgoing>
    </serviceTask>
    <sequenceFlow id=""Flow_CancelEnd"" sourceRef=""ServiceTask_Cancel"" targetRef=""EndEvent_Cancelled"" />
    <endEvent id=""EndEvent_Cancelled"" name=""Process Cancelled"">
      <incoming>Flow_CancelEnd</incoming>
    </endEvent>
    
    <sequenceFlow id=""Flow_3"" sourceRef=""UserTask_2"" targetRef=""UserTask_3"" />
    
    <!-- Third user task with an interrupting escalation boundary event -->
    <userTask id=""UserTask_3"" name=""Final Review"">
      <incoming>Flow_3</incoming>
      <outgoing>Flow_4</outgoing>
    </userTask>
    <boundaryEvent id=""EscalationBoundary_1"" name=""Escalation Required"" attachedToRef=""UserTask_3"" cancelActivity=""true"">
      <outgoing>Flow_Escalate</outgoing>
      <escalationEventDefinition escalationRef=""Escalation_High_Priority"" />
    </boundaryEvent>
    <sequenceFlow id=""Flow_Escalate"" sourceRef=""EscalationBoundary_1"" targetRef=""UserTask_Escalated"" />
    <userTask id=""UserTask_Escalated"" name=""Executive Review"">
      <incoming>Flow_Escalate</incoming>
      <outgoing>Flow_EscalateComplete</outgoing>
    </userTask>
    <sequenceFlow id=""Flow_EscalateComplete"" sourceRef=""UserTask_Escalated"" targetRef=""EndEvent_Escalated"" />
    <endEvent id=""EndEvent_Escalated"" name=""Process Escalated and Completed"">
      <incoming>Flow_EscalateComplete</incoming>
    </endEvent>
    
    <sequenceFlow id=""Flow_4"" sourceRef=""UserTask_3"" targetRef=""EndEvent_1"" />
    <endEvent id=""EndEvent_1"" name=""Normal Completion"">
      <incoming>Flow_4</incoming>
    </endEvent>
  </process>
  
  <!-- Message definitions -->
  <message id=""Message_Urgent_Cancel"" name=""UrgentCancellation"" />
  
  <!-- Escalation definitions -->
  <escalation id=""Escalation_High_Priority"" name=""HighPriorityEscalation"" escalationCode=""HIGH_PRIORITY"" />
</definitions>";

    /// <summary>
    /// Run the user task example
    /// </summary>
    public static async Task RunAsync()
    {
        // Create and start the host with all required services
        var host = CreateHostBuilder().Build();
        await host.StartAsync();
        
        var logger = host.Services.GetRequiredService<ILogger<UserTaskExample>>();
        var bpmnProcessor = host.Services.GetRequiredService<BpmnProcessorService>();
        var userTaskService = host.Services.GetRequiredService<IUserTaskService>();
        var eventBus = host.Services.GetRequiredService<IEventBus>();

        logger.LogInformation("Starting User Task Example with Boundary Events");

        try
        {
            // Deploy the process definition
            string deploymentKey = "user-task-flow-with-boundaries";
            string definitionId = await bpmnProcessor.DeployProcessDefinitionAsync(
                deploymentKey, 
                UserTaskFlowXml,
                "User Task Workflow Example with Boundary Events");
                
            logger.LogInformation("Deployed process definition with ID {ProcessDefinitionId}", 
                definitionId);

            // Create a new process instance
            var processInstanceId = await bpmnProcessor.StartProcessInstanceAsync(
                deploymentKey, 
                null,
                new Dictionary<string, object> 
                { 
                    { "requester", "John Doe" },
                    { "priority", 1 }
                });
                
            logger.LogInformation("Created process instance with ID {ProcessInstanceId}", 
                processInstanceId);

            // Short pause to ensure tasks are created
            await Task.Delay(2000);

            // Display process tasks
            var tasks = await userTaskService.GetTasksByProcessInstanceAsync(processInstanceId);
            
            logger.LogInformation("Found {TaskCount} tasks for the process", tasks.Count);
            foreach (var task in tasks)
            {
                logger.LogInformation("Task: {TaskId}, Title: {Title}, Status: {Status}", 
                    task.TaskId, task.TaskTitle, task.Status);
            }

            if (tasks.Count > 0)
            {
                var firstTask = tasks[0];
                
                // Assign task to a user
                logger.LogInformation("Assigning task {TaskId} to user user1", firstTask.TaskId);
                await userTaskService.AssignTaskAsync(firstTask.TaskId, "user1", "John Smith");
                
                // Wait for the non-interrupting timer boundary event to trigger
                logger.LogInformation("Waiting for non-interrupting timer boundary event (10 seconds)...");
                await Task.Delay(12000); // Wait a bit more than the 10 seconds timer
                
                logger.LogInformation("Timer should have triggered. The main task should still be active.");
                
                // Check if the first task is still active (it should be, as the boundary event is non-interrupting)
                var activeTask = await userTaskService.GetTaskByIdAsync(firstTask.TaskId);
                logger.LogInformation("First task status after timer: {Status}", activeTask?.Status);
                
                // Complete the first task
                logger.LogInformation("Completing task {TaskId}", firstTask.TaskId);
                await userTaskService.CompleteTaskAsync(
                    firstTask.TaskId, 
                    "user1", 
                    new Dictionary<string, object>
                    {
                        { "approved", true },
                        { "comment", "Request approved" }
                    });
                
                // Short pause to ensure task completion is processed
                await Task.Delay(1000);
                
                // Check for new tasks (second task)
                tasks = await userTaskService.GetTasksByProcessInstanceAsync(processInstanceId);
                
                logger.LogInformation("After first task completion, {TaskCount} active tasks remain", 
                    tasks.Count);
                
                if (tasks.Count > 0)
                {
                    var secondTask = tasks[0]; // Now the second task is the first active one
                    
                    logger.LogInformation("Assigning task {TaskId} to user manager1", secondTask.TaskId);
                    await userTaskService.AssignTaskAsync(secondTask.TaskId, "manager1", "Manager One");
                    
                    // Trigger the interrupting message boundary event
                    logger.LogInformation("Triggering interrupting message boundary event...");
                    await eventBus.PublishAsync(new Events.MessageReceivedEvent
                    {
                        EventId = Guid.NewGuid(),
                        ProcessInstanceId = processInstanceId,
                        MessageEventId = Guid.NewGuid().ToString(),
                        MessageName = "UrgentCancellation",
                        MessageContent = new Dictionary<string, object>
                        {
                            { "cancelReason", "Urgent business reason" }
                        },
                        Intent = "RECEIVED",
                        Timestamp = DateTime.UtcNow
                    });
                    
                    // Wait for event processing
                    await Task.Delay(2000);
                    
                    // Check if the second task was cancelled (it should be, as the boundary event is interrupting)
                    var cancelledTask = await userTaskService.GetTaskByIdAsync(secondTask.TaskId);
                    logger.LogInformation("Second task status after message: {Status}", cancelledTask?.Status);
                    
                    // The process should have followed the cancellation path
                    // Check process state
                    var processState = await bpmnProcessor.GetProcessInstanceStateAsync(processInstanceId);
                    
                    logger.LogInformation("Process state after cancellation: Active elements: {ActiveCount}, Status: {Status}",
                        processState.ActiveElements.Count,
                        processState.Status);
                }
                
                // Demonstrate the escalation boundary event with a new process instance
                logger.LogInformation("Creating a new process instance to demonstrate escalation boundary event");
                
                var escalationProcessId = await bpmnProcessor.StartProcessInstanceAsync(
                    deploymentKey, 
                    null,
                    new Dictionary<string, object> 
                    { 
                        { "requester", "Jane Smith" },
                        { "priority", 3 }
                    });
                
                // Short pause to ensure tasks are created
                await Task.Delay(2000);
                
                // Get tasks for the new process
                var escalationTasks = await userTaskService.GetTasksByProcessInstanceAsync(escalationProcessId);
                
                // Complete first task
                if (escalationTasks.Count > 0)
                {
                    await userTaskService.CompleteTaskAsync(
                        escalationTasks[0].TaskId,
                        "user2",
                        new Dictionary<string, object> { { "approved", true } });
                }
                
                // Wait for task transition
                await Task.Delay(1000);
                
                // Get second task and complete it
                escalationTasks = await userTaskService.GetTasksByProcessInstanceAsync(escalationProcessId);
                if (escalationTasks.Count > 0)
                {
                    await userTaskService.CompleteTaskAsync(
                        escalationTasks[0].TaskId,
                        "manager2",
                        new Dictionary<string, object> { { "approved", true } });
                }
                
                // Wait for task transition
                await Task.Delay(1000);
                
                // Get third task
                escalationTasks = await userTaskService.GetTasksByProcessInstanceAsync(escalationProcessId);
                if (escalationTasks.Count > 0)
                {
                    var thirdTask = escalationTasks[0];
                    
                    // Trigger the escalation boundary event
                    logger.LogInformation("Triggering interrupting escalation boundary event...");
                    
                    // For escalation, we need to publish an escalation event
                    await eventBus.PublishAsync(new Events.MessageReceivedEvent
                    {
                        EventId = Guid.NewGuid(),
                        ProcessInstanceId = escalationProcessId,
                        MessageEventId = Guid.NewGuid().ToString(),
                        MessageName = "escalation:HIGH_PRIORITY",
                        MessageContent = new Dictionary<string, object>
                        {
                            { "escalationReason", "Executive attention required" }
                        },
                        Intent = "TRIGGERED",
                        Timestamp = DateTime.UtcNow
                    });
                    
                    // Wait for event processing
                    await Task.Delay(2000);
                    
                    // Check if the third task was cancelled and escalated
                    var escalatedTask = await userTaskService.GetTaskByIdAsync(thirdTask.TaskId);
                    logger.LogInformation("Third task status after escalation: {Status}", escalatedTask?.Status);
                    
                    // Check for the executive review task (the escalation path)
                    var executiveTasks = await userTaskService.GetTasksByProcessInstanceAsync(escalationProcessId);
                    
                    logger.LogInformation("Tasks after escalation: {Count}", executiveTasks.Count);
                    foreach (var task in executiveTasks)
                    {
                        logger.LogInformation("Post-escalation task: {TaskId}, Title: {Title}", 
                            task.TaskId, task.TaskTitle);
                    }
                }

                var process = await bpmnProcessor.GetProcessInstanceStateAsync(processInstanceId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing user task example");
        }
        finally
        {
            // Stop the host
            await host.StopAsync();
        }

        logger.LogInformation("User Task Example with Boundary Events completed");
    }
    
    /// <summary>
    /// Create the host builder with all required services
    /// </summary>
    private static IHostBuilder CreateHostBuilder()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Debug);
            })
            .ConfigureServices((_, services) =>
            {
                // Register Event Sourcing services
                services.AddBpmnEventSourcing(options => {
                    // Use auto-registration from the assembly
                    options.AutoRegisterEventHandlers = true;
                });
                
                // No need to register handlers explicitly now, as they're auto-registered
                // from the Novin.Bpmn.EventSourcing assembly
            });
    }
} 