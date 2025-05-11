using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core;
using Novin.Bpmn.EventSourcing.Core.Models;
using Novin.Bpmn.EventSourcing.Events;
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

    private const string DeploymentKey = "user-task-flow-with-boundaries";
    private const int TaskCreationDelay = 2000;
    private const int TaskTransitionDelay = 1000;
    private const int TimerBoundaryDelay = 12000;
    private const int MaxRetries = 3;
    private const int RetryDelay = 1000;

    /// <summary>
    /// Run the user task example
    /// </summary>
    public static async Task RunAsync()
    {
        IHost? host = null;
        try
        {
            // Create and start the host with all required services
            host = CreateHostBuilder().Build();
            await host.StartAsync();
            
            var logger = host.Services.GetRequiredService<ILogger<UserTaskExample>>();
            var bpmnProcessor = host.Services.GetRequiredService<BpmnService>();
            var userTaskService = host.Services.GetRequiredService<IUserTaskService>();
            var eventBus = host.Services.GetRequiredService<IEventBus>();

            logger.LogInformation("Starting User Task Example with Boundary Events");

            // Deploy the process definition
            string definitionId = await DeployProcessDefinitionAsync(bpmnProcessor, logger);
            
            // Run the normal flow example
            await RunNormalFlowExampleAsync(bpmnProcessor, userTaskService, eventBus, logger);
            
            // Run the escalation flow example
            await RunEscalationFlowExampleAsync(bpmnProcessor, userTaskService, eventBus, logger);
        }
        catch (Exception ex)
        {
            if (host?.Services.GetService<ILogger<UserTaskExample>>() is ILogger<UserTaskExample> logger)
            {
                logger.LogError(ex, "Error executing user task example");
            }
            throw;
        }
        finally
        {
            if (host != null)
            {
                await host.StopAsync();
            }
        }
    }

    private static async Task<string> DeployProcessDefinitionAsync(
        BpmnService bpmnProcessor,
        ILogger<UserTaskExample> logger)
    {
        var  definition = await bpmnProcessor.DeployProcessDefinitionAsync(
            DeploymentKey, 
            UserTaskFlowXml,
            "User Task Workflow Example with Boundary Events");
            
        logger.LogInformation("Deployed process definition with ID {ProcessDefinitionId}", 
            definition.DefinitionId);
            
        return definition.DefinitionId;
    }

    private static async Task RunNormalFlowExampleAsync(
        BpmnService bpmnProcessor,
        IUserTaskService userTaskService,
        IEventBus eventBus,
        ILogger<UserTaskExample> logger)
    {
        // Create a new process instance with retry logic
        var processInstanceId = await StartProcessInstanceWithRetryAsync(
            bpmnProcessor,
            new Dictionary<string, object> 
            { 
                { "requester", "John Doe" },
                { "priority", 1 }
            },
            logger);
            
        logger.LogInformation("Created process instance with ID {ProcessInstanceId}", 
            processInstanceId);

        // Wait for tasks to be created
        await Task.Delay(TaskCreationDelay);

        // Get and display initial tasks
        var tasks = await GetAndDisplayTasksAsync(userTaskService, processInstanceId, logger);
        
        if (tasks.Count > 0)
        {
            var firstTask = tasks[0];
            
            // Handle first task
            await HandleFirstTaskAsync(userTaskService, firstTask, logger);
            
            // Handle second task with message boundary
            await HandleSecondTaskWithMessageBoundaryAsync(
                bpmnProcessor, userTaskService, eventBus, processInstanceId, logger);
        }
    }

    private static async Task<string> StartProcessInstanceWithRetryAsync(
        BpmnService bpmnProcessor,
        Dictionary<string, object> variables,
        ILogger<UserTaskExample> logger)
    {
        int retryCount = 0;
        while (true)
        {
            try
            {
                return await bpmnProcessor.StartProcessInstanceAsync(
                    DeploymentKey,
                    null,
                    variables);
            }
            catch (Exception ex) when (ex.Message.Contains("Concurrency conflict") && retryCount < MaxRetries)
            {
                retryCount++;
                logger.LogWarning("Concurrency conflict detected, retry {RetryCount} of {MaxRetries}", 
                    retryCount, MaxRetries);
                await Task.Delay(RetryDelay * retryCount);
            }
        }
    }

    private static async Task<List<UserTaskInfo>> GetAndDisplayTasksAsync(
        IUserTaskService userTaskService,
        string processInstanceId,
        ILogger<UserTaskExample> logger)
    {
        var tasks = await userTaskService.GetTasksByProcessInstanceAsync(processInstanceId);
        
        logger.LogInformation("Found {TaskCount} tasks for the process", tasks.Count);
        foreach (var task in tasks)
        {
            logger.LogInformation("Task: {TaskId}, Title: {Title}, Status: {Status}", 
                task.TaskId, task.TaskTitle, task.Status);
        }
        
        return tasks;
    }

    private static async Task HandleFirstTaskAsync(
        IUserTaskService userTaskService,
        UserTaskInfo task,
        ILogger<UserTaskExample> logger)
    {
        // Assign task to a user
        logger.LogInformation("Assigning task {TaskId} to user user1", task.TaskId);
        await userTaskService.AssignTaskAsync(task.TaskId, "user1", "John Smith");
        
        // Wait for the non-interrupting timer boundary event to trigger
        logger.LogInformation("Waiting for non-interrupting timer boundary event (10 seconds)...");
        await Task.Delay(TimerBoundaryDelay);
        
        logger.LogInformation("Timer should have triggered. The main task should still be active.");
        
        // Check if the first task is still active
        var activeTask = await userTaskService.GetTaskByIdAsync(task.TaskId);
        logger.LogInformation("First task status after timer: {Status}", activeTask?.Status);
        
        // Complete the first task
        logger.LogInformation("Completing task {TaskId}", task.TaskId);
        await userTaskService.CompleteTaskAsync(
            task.TaskId, 
            "user1", 
            new Dictionary<string, object>
            {
                { "approved", true },
                { "comment", "Request approved" }
            });
        
        // Wait for task completion to be processed
        await Task.Delay(TaskTransitionDelay);
    }

    private static async Task HandleSecondTaskWithMessageBoundaryAsync(
        BpmnService bpmnProcessor,
        IUserTaskService userTaskService,
        IEventBus eventBus,
        string processInstanceId,
        ILogger<UserTaskExample> logger)
    {
        // Get tasks after first task completion
        var tasks = await userTaskService.GetTasksByProcessInstanceAsync(processInstanceId);
        
        logger.LogInformation("After first task completion, {TaskCount} active tasks remain", 
            tasks.Count);
        
        if (tasks.Count > 0)
        {
            var secondTask = tasks[0];
            
            logger.LogInformation("Assigning task {TaskId} to user manager1", secondTask.TaskId);
            await userTaskService.AssignTaskAsync(secondTask.TaskId, "manager1", "Manager One");
            
            // Trigger the interrupting message boundary event
            logger.LogInformation("Triggering interrupting message boundary event...");
            await eventBus.PublishAsync(new ElementProcessing
            {
                EventId = Guid.NewGuid(),
                ProcessInstanceId = processInstanceId,
                ElementId = "MessageBoundary_1",
                ElementType = "boundaryEvent",
                Progress = 100,
                ProcessingDetails = "Message boundary event triggered",
                Timestamp = DateTime.UtcNow
            });
            
            // Wait for event processing
            await Task.Delay(TaskTransitionDelay);
            
            // Check if the second task was cancelled
            var cancelledTask = await userTaskService.GetTaskByIdAsync(secondTask.TaskId);
            logger.LogInformation("Second task status after message: {Status}", cancelledTask?.Status);
            
            // Check process state
            var processState = await bpmnProcessor.GetProcessInstanceStateAsync(processInstanceId);
            
            logger.LogInformation("Process state after cancellation: Active elements: {ActiveCount}, Status: {Status}",
                processState.ActiveElements.Count,
                processState.Status);
        }
    }

    private static async Task RunEscalationFlowExampleAsync(
        BpmnService bpmnProcessor,
        IUserTaskService userTaskService,
        IEventBus eventBus,
        ILogger<UserTaskExample> logger)
    {
        logger.LogInformation("Creating a new process instance to demonstrate escalation boundary event");
        
        var escalationProcessId = await StartProcessInstanceWithRetryAsync(
            bpmnProcessor,
            new Dictionary<string, object> 
            { 
                { "requester", "Jane Smith" },
                { "priority", 3 }
            },
            logger);
        
        // Wait for tasks to be created
        await Task.Delay(TaskCreationDelay);
        
        // Complete first two tasks
        await CompleteFirstTwoTasksAsync(userTaskService, escalationProcessId, logger);
        
        // Handle escalation
        await HandleEscalationAsync(
            bpmnProcessor, userTaskService, eventBus, escalationProcessId, logger);
    }

    private static async Task CompleteFirstTwoTasksAsync(
        IUserTaskService userTaskService,
        string processInstanceId,
        ILogger<UserTaskExample> logger)
    {
        // Get and complete first task
        var tasks = await userTaskService.GetTasksByProcessInstanceAsync(processInstanceId);
        if (tasks.Count > 0)
        {
            await userTaskService.CompleteTaskAsync(
                tasks[0].TaskId,
                "user2",
                new Dictionary<string, object> { { "approved", true } });
        }
        
        // Wait for task transition
        await Task.Delay(TaskTransitionDelay);
        
        // Get and complete second task
        tasks = await userTaskService.GetTasksByProcessInstanceAsync(processInstanceId);
        if (tasks.Count > 0)
        {
            await userTaskService.CompleteTaskAsync(
                tasks[0].TaskId,
                "manager2",
                new Dictionary<string, object> { { "approved", true } });
        }
        
        // Wait for task transition
        await Task.Delay(TaskTransitionDelay);
    }

    private static async Task HandleEscalationAsync(
        BpmnService bpmnProcessor,
        IUserTaskService userTaskService,
        IEventBus eventBus,
        string processInstanceId,
        ILogger<UserTaskExample> logger)
    {
        // Get third task
        var tasks = await userTaskService.GetTasksByProcessInstanceAsync(processInstanceId);
        if (tasks.Count > 0)
        {
            var thirdTask = tasks[0];
            
            // Trigger the escalation boundary event
            logger.LogInformation("Triggering interrupting escalation boundary event...");
            
            await eventBus.PublishAsync(new ElementProcessing
            {
                EventId = Guid.NewGuid(),
                ProcessInstanceId = processInstanceId,
                ElementId = "EscalationBoundary_1",
                ElementType = "boundaryEvent",
                Progress = 100,
                ProcessingDetails = "Escalation boundary event triggered",
                Timestamp = DateTime.UtcNow
            });
            
            // Wait for event processing
            await Task.Delay(TaskTransitionDelay);
            
            // Check if the third task was cancelled and escalated
            var escalatedTask = await userTaskService.GetTaskByIdAsync(thirdTask.TaskId);
            logger.LogInformation("Third task status after escalation: {Status}", escalatedTask?.Status);
            
            // Check for the executive review task
            var executiveTasks = await userTaskService.GetTasksByProcessInstanceAsync(processInstanceId);
            
            logger.LogInformation("Tasks after escalation: {Count}", executiveTasks.Count);
            foreach (var task in executiveTasks)
            {
                logger.LogInformation("Post-escalation task: {TaskId}, Title: {Title}", 
                    task.TaskId, task.TaskTitle);
            }
        }
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
                    options.AutoRegisterEventHandlers = true;
                });
            });
    }
} 