using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing.Examples;

/// <summary>
/// مثال استفاده از BpmnProcessorService
/// </summary>
public class BpmnProcessorExample
{
    // مثال ساده از یک فرآیند BPMN با یک وظیفه کاربری
    private const string SampleBpmnXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
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
    <sequenceFlow id=""Flow_1"" sourceRef=""StartEvent_1"" targetRef=""Task_1"" />
    <userTask id=""Task_1"" name=""User Task"">
      <incoming>Flow_1</incoming>
      <outgoing>Flow_2</outgoing>
    </userTask>
    <sequenceFlow id=""Flow_2"" sourceRef=""Task_1"" targetRef=""EndEvent_1"" />
    <endEvent id=""EndEvent_1"" name=""End"">
      <incoming>Flow_2</incoming>
    </endEvent>
  </process>
</definitions>";

    // مثال پیچیده‌تر از یک فرآیند BPMN با گیت‌وی شرطی و چندین وظیفه کاربری
    private const string AdvancedBpmnXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<definitions xmlns=""http://www.omg.org/spec/BPMN/20100524/MODEL"" 
             xmlns:bpmndi=""http://www.omg.org/spec/BPMN/20100524/DI"" 
             xmlns:dc=""http://www.omg.org/spec/DD/20100524/DC"" 
             xmlns:di=""http://www.omg.org/spec/DD/20100524/DI"" 
             id=""Definitions_1"" 
             targetNamespace=""http://bpmn.io/schema/bpmn"">
  <process id=""LoanApprovalProcess"" name=""Loan Approval Process"" isExecutable=""true"">
    <startEvent id=""StartEvent_1"" name=""Loan Request Received"">
      <outgoing>Flow_1</outgoing>
    </startEvent>
    <sequenceFlow id=""Flow_1"" sourceRef=""StartEvent_1"" targetRef=""ReviewTask"" />
    
    <userTask id=""ReviewTask"" name=""Review Application"">
      <incoming>Flow_1</incoming>
      <outgoing>Flow_2</outgoing>
    </userTask>
    <sequenceFlow id=""Flow_2"" sourceRef=""ReviewTask"" targetRef=""ApprovalGateway"" />
    
    <exclusiveGateway id=""ApprovalGateway"" name=""Application Approved?"">
      <incoming>Flow_2</incoming>
      <outgoing>Flow_Approved</outgoing>
      <outgoing>Flow_Rejected</outgoing>
    </exclusiveGateway>
    
    <sequenceFlow id=""Flow_Approved"" name=""Yes"" sourceRef=""ApprovalGateway"" targetRef=""ProcessLoanTask"">
      <conditionExpression>${approved == true}</conditionExpression>
    </sequenceFlow>
    
    <sequenceFlow id=""Flow_Rejected"" name=""No"" sourceRef=""ApprovalGateway"" targetRef=""RejectionTask"">
      <conditionExpression>${approved == false}</conditionExpression>
    </sequenceFlow>
    
    <userTask id=""ProcessLoanTask"" name=""Process Loan"">
      <incoming>Flow_Approved</incoming>
      <outgoing>Flow_3</outgoing>
    </userTask>
    <sequenceFlow id=""Flow_3"" sourceRef=""ProcessLoanTask"" targetRef=""EndEvent_Approved"" />
    
    <userTask id=""RejectionTask"" name=""Send Rejection"">
      <incoming>Flow_Rejected</incoming>
      <outgoing>Flow_4</outgoing>
    </userTask>
    <sequenceFlow id=""Flow_4"" sourceRef=""RejectionTask"" targetRef=""EndEvent_Rejected"" />
    
    <endEvent id=""EndEvent_Approved"" name=""Loan Approved"">
      <incoming>Flow_3</incoming>
    </endEvent>
    
    <endEvent id=""EndEvent_Rejected"" name=""Loan Rejected"">
      <incoming>Flow_4</incoming>
    </endEvent>
  </process>
</definitions>";

    /// <summary>
    /// اجرای مثال پردازش BPMN
    /// </summary>
    public static async Task RunAsync()
    {
        // ساخت سرویسها
        var host = CreateHostBuilder().Build();
        
        // شروع خدمات پس‌زمینه
        await host.StartAsync();
        
        try
        {
            // دریافت سرویس پردازش BPMN
            var bpmnProcessor = host.Services.GetRequiredService<BpmnProcessorService>();
            var logger = host.Services.GetRequiredService<ILogger<BpmnProcessorExample>>();
            
            // نصب تعریف فرآیند
            string deploymentKey = "sample-process";
            logger.LogInformation("Deploying BPMN process definition with key {DeploymentKey}", deploymentKey);
            
            var definitionId = await bpmnProcessor.DeployProcessDefinitionAsync(
                deploymentKey, 
                SampleBpmnXml, 
                "Sample Process");
                
            logger.LogInformation("Deployed BPMN process with definition ID {DefinitionId}", definitionId);
            
            // نصب تعریف فرآیند پیشرفته
            string advancedDeploymentKey = "loan-approval-process";
            logger.LogInformation("Deploying advanced BPMN process definition with key {DeploymentKey}", advancedDeploymentKey);
            
            var advancedDefinitionId = await bpmnProcessor.DeployProcessDefinitionAsync(
                advancedDeploymentKey, 
                AdvancedBpmnXml, 
                "Loan Approval Process");
                
            logger.LogInformation("Deployed advanced BPMN process with definition ID {AdvancedDefinitionId}", advancedDefinitionId);
            
            // کمی صبر برای اطمینان از ثبت تعریف
            await Task.Delay(500);
            
            // نمایش منوی انتخاب مثال
            Console.WriteLine("\nChoose an example to run:");
            Console.WriteLine("1. Simple Process with User Task");
            Console.WriteLine("2. Advanced Loan Approval Process");
            Console.Write("Enter your choice (1-2): ");
            
            string choice = Console.ReadLine() ?? "1";
            
            switch (choice)
            {
                case "2":
                    await RunAdvancedProcessExampleAsync(bpmnProcessor, logger);
                    break;
                    
                case "1":
                default:
                    await RunSimpleProcessExampleAsync(bpmnProcessor, logger);
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Console.ResetColor();
        }
        finally
        {
            // توقف خدمات پس‌زمینه
            await host.StopAsync();
        }
    }
    
    /// <summary>
    /// اجرای مثال فرآیند ساده با یک وظیفه کاربری
    /// </summary>
    private static async Task RunSimpleProcessExampleAsync(BpmnProcessorService bpmnProcessor, ILogger logger)
    {
        // شروع یک نمونه فرآیند
        logger.LogInformation("Starting a simple process instance");
        
        var variables = new Dictionary<string, object>
        {
            { "applicant", "John Doe" },
            { "amount", 5000 }
        };
        
        var processInstanceId = await bpmnProcessor.StartProcessInstanceAsync(
            "sample-process", 
            "Process_1", 
            variables);
            
        logger.LogInformation("Started process instance with ID {ProcessInstanceId}", processInstanceId);
        
        // کمی صبر برای پردازش رویدادها
        await Task.Delay(500);
        
        // بررسی وضعیت فرآیند
        var state = await bpmnProcessor.GetProcessInstanceStateAsync(processInstanceId);
        
        logger.LogInformation("Process instance status: {Status}", state.Status);
        logger.LogInformation("Active elements: {ActiveElements}", string.Join(", ", state.ActiveElements));
        logger.LogInformation("Process variables: {Variables}", 
            System.Text.Json.JsonSerializer.Serialize(state.Variables));
            
        // بررسی وظایف کاربری فعال
        var tasks = await bpmnProcessor.GetUserTasksForProcessInstanceAsync(processInstanceId);
        
        if (tasks.Count == 0)
        {
            logger.LogWarning("No active user tasks found!");
            return;
        }
        
        logger.LogInformation("Found {Count} active user tasks:", tasks.Count);
        
        foreach (var task in tasks)
        {
            logger.LogInformation("Task ID: {TaskId}, Title: {TaskTitle}, Assignee: {Assignee}",
                task.Key,
                task.Value.TaskTitle ?? task.Key,
                task.Value.Assignee ?? "Unassigned");
                
            // نمایش اطلاعات وظیفه
            Console.WriteLine($"\nUser Task: {task.Value.TaskTitle ?? task.Key}");
            Console.WriteLine($"Description: {task.Value.TaskDescription ?? "No description"}");
            Console.WriteLine($"Status: {task.Value.Status}");
            Console.WriteLine("Form variables:");
            
            if (task.Value.FormVariables != null && task.Value.FormVariables.Any())
            {
                foreach (var variable in task.Value.FormVariables)
                {
                    Console.WriteLine($"  {variable.Key}: {variable.Value}");
                }
            }
            else
            {
                Console.WriteLine("  No form variables");
            }
            
            Console.WriteLine("\nDo you want to complete this task? (y/n)");
            string response = Console.ReadLine() ?? "y";
            
            if (response.ToLower() == "y")
            {
                Console.WriteLine("Enter data for the form (in format key=value, empty line to finish):");
                
                var formData = new Dictionary<string, object>();
                
                string line;
                while (!string.IsNullOrWhiteSpace(line = Console.ReadLine() ?? ""))
                {
                    var parts = line.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        formData[parts[0].Trim()] = parts[1].Trim();
                    }
                }
                
                logger.LogInformation("Completing user task 'Task_1'");
                
                await bpmnProcessor.CompleteUserTaskAsync(
                    processInstanceId, 
                    task.Key, 
                    formData);
                    
                logger.LogInformation("User task completed");
                
                // کمی صبر برای پردازش رویدادها
                await Task.Delay(500);
                
                // بررسی وضعیت نهایی
                state = await bpmnProcessor.GetProcessInstanceStateAsync(processInstanceId);
                
                logger.LogInformation("Final process status: {Status}", state.Status);
                logger.LogInformation("Active elements: {ActiveElements}", 
                    state.ActiveElements.Count > 0 ? string.Join(", ", state.ActiveElements) : "None");
                logger.LogInformation("Completed elements: {CompletedElements}", 
                    string.Join(", ", state.CompletedElements));
                logger.LogInformation("Final variables: {Variables}", 
                    System.Text.Json.JsonSerializer.Serialize(state.Variables));
            }
            else
            {
                logger.LogInformation("User chose not to complete the task");
            }
        }
        
        logger.LogInformation("Simple process example completed");
    }
    
    /// <summary>
    /// اجرای مثال فرآیند پیشرفته وام با گیت‌وی شرطی و چندین وظیفه کاربری
    /// </summary>
    private static async Task RunAdvancedProcessExampleAsync(BpmnProcessorService bpmnProcessor, ILogger logger)
    {
        // شروع یک نمونه فرآیند
        logger.LogInformation("Starting a loan approval process instance");
        
        var variables = new Dictionary<string, object>
        {
            { "applicant", "Jane Smith" },
            { "amount", 15000 },
            { "creditScore", 720 },
            { "income", 65000 }
        };
        
        var processInstanceId = await bpmnProcessor.StartProcessInstanceAsync(
            "loan-approval-process", 
            "LoanApprovalProcess", 
            variables);
            
        logger.LogInformation("Started loan approval process instance with ID {ProcessInstanceId}", processInstanceId);
        
        // کمی صبر برای پردازش رویدادها
        await Task.Delay(500);
        
        // بررسی وضعیت فرآیند
        var state = await bpmnProcessor.GetProcessInstanceStateAsync(processInstanceId);
        
        logger.LogInformation("Process instance status: {Status}", state.Status);
        logger.LogInformation("Active elements: {ActiveElements}", string.Join(", ", state.ActiveElements));
        
        // چرخه اصلی برای مدیریت وظایف کاربری
        bool processComplete = false;
        
        while (!processComplete)
        {
            // بررسی وضعیت فرآیند
            state = await bpmnProcessor.GetProcessInstanceStateAsync(processInstanceId);
            
            if (state.Status == ProcessStatus.Completed)
            {
                logger.LogInformation("Process has completed!");
                processComplete = true;
                continue;
            }
            
            // بررسی وظایف کاربری فعال
            var tasks = await bpmnProcessor.GetUserTasksForProcessInstanceAsync(processInstanceId);
            
            if (tasks.Count == 0)
            {
                logger.LogInformation("No active user tasks found. Checking if process is still running...");
                if (state.Status == ProcessStatus.Running && state.ActiveElements.Count > 0)
                {
                    logger.LogInformation("Process is still running. Waiting for user tasks to be created...");
                    await Task.Delay(1000);
                    continue;
                }
                else
                {
                    logger.LogInformation("Process is no longer running or has no active elements.");
                    processComplete = true;
                    continue;
                }
            }
            
            logger.LogInformation("Found {Count} active user tasks:", tasks.Count);
            
            // تخصیص و تکمیل هر وظیفه کاربری فعال
            foreach (var task in tasks)
            {
                logger.LogInformation("Task ID: {TaskId}, Title: {TaskTitle}, Assignee: {Assignee}",
                    task.Key,
                    task.Value.TaskTitle ?? task.Key,
                    task.Value.Assignee ?? "Unassigned");
                    
                // تخصیص وظیفه به کاربر فعلی اگر تخصیص داده نشده باشد
                if (string.IsNullOrEmpty(task.Value.Assignee))
                {
                    var userId = "current-user";
                    logger.LogInformation("Claiming task {TaskId} for user {UserId}", task.Key, userId);
                    
                    await bpmnProcessor.ClaimUserTaskAsync(
                        processInstanceId,
                        task.Key,
                        userId,
                        "Current User");
                        
                    logger.LogInformation("Task claimed successfully");
                }
                
                // نمایش اطلاعات وظیفه
                Console.WriteLine($"\nUser Task: {task.Value.TaskTitle ?? task.Key}");
                Console.WriteLine($"Description: {task.Value.TaskDescription ?? "No description"}");
                Console.WriteLine($"Status: {task.Value.Status}");
                Console.WriteLine($"Assignee: {task.Value.Assignee ?? "Unassigned"}");
                
                // نمایش متغیرهای فعلی فرآیند
                Console.WriteLine("\nCurrent process variables:");
                foreach (var variable in state.Variables)
                {
                    Console.WriteLine($"  {variable.Key}: {variable.Value}");
                }
                
                Console.WriteLine("\nDo you want to complete this task? (y/n)");
                string response = Console.ReadLine() ?? "y";
                
                if (response.ToLower() == "y")
                {
                    var formData = new Dictionary<string, object>();
                    
                    // تنظیم داده‌های فرم بسته به نوع وظیفه
                    if (task.Key == "ReviewTask")
                    {
                        Console.WriteLine("Approve the loan application? (y/n)");
                        string approval = Console.ReadLine() ?? "y";
                        formData["approved"] = approval.ToLower() == "y";
                        
                        if (approval.ToLower() == "y")
                        {
                            Console.WriteLine("Enter approval notes:");
                            formData["approvalNotes"] = Console.ReadLine() ?? "Application looks good";
                        }
                        else
                        {
                            Console.WriteLine("Enter rejection reason:");
                            formData["rejectionReason"] = Console.ReadLine() ?? "Application does not meet criteria";
                        }
                    }
                    else if (task.Key == "ProcessLoanTask")
                    {
                        Console.WriteLine("Enter loan reference number:");
                        formData["loanReferenceNumber"] = Console.ReadLine() ?? $"LN-{DateTime.Now.Ticks.ToString().Substring(0, 10)}";
                        
                        Console.WriteLine("Enter disbursement date (YYYY-MM-DD):");
                        formData["disbursementDate"] = Console.ReadLine() ?? DateTime.Now.AddDays(7).ToString("yyyy-MM-dd");
                    }
                    else if (task.Key == "RejectionTask")
                    {
                        Console.WriteLine("Enter notification method (email/mail/phone):");
                        formData["notificationMethod"] = Console.ReadLine() ?? "email";
                        
                        Console.WriteLine("Was the applicant notified? (y/n)");
                        formData["wasNotified"] = (Console.ReadLine() ?? "y").ToLower() == "y";
                    }
                    
                    logger.LogInformation("Completing user task '{TaskId}'", task.Key);
                    
                    await bpmnProcessor.CompleteUserTaskAsync(
                        processInstanceId, 
                        task.Key, 
                        formData);
                        
                    logger.LogInformation("User task completed");
                    
                    // کمی صبر برای پردازش رویدادها
                    await Task.Delay(500);
                    break; // از حلقه فور خارج می‌شویم تا در حلقه وایل وضعیت جدید را بررسی کنیم
                }
                else
                {
                    logger.LogInformation("User chose not to complete the task");
                }
            }
            
            // کمی صبر قبل از بررسی مجدد
            await Task.Delay(500);
        }
        
        // بررسی وضعیت نهایی
        state = await bpmnProcessor.GetProcessInstanceStateAsync(processInstanceId);
        
        logger.LogInformation("Final process status: {Status}", state.Status);
        logger.LogInformation("Active elements: {ActiveElements}", 
            state.ActiveElements.Count > 0 ? string.Join(", ", state.ActiveElements) : "None");
        logger.LogInformation("Completed elements: {CompletedElements}", 
            string.Join(", ", state.CompletedElements));
        logger.LogInformation("Final variables: {Variables}", 
            System.Text.Json.JsonSerializer.Serialize(state.Variables));
            
        logger.LogInformation("Advanced process example completed");
    }
    
    /// <summary>
    /// ساخت میزبان برنامه
    /// </summary>
    private static IHostBuilder CreateHostBuilder()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            })
            .ConfigureServices((_, services) =>
            {
                // ثبت سرویس‌های Event Sourcing
                services.AddBpmnEventSourcing();
                
                // جستجوی خودکار پردازش‌کننده‌های رویداد در اسمبلی فعلی
                services.AddBpmnEventHandlers(typeof(BpmnProcessorExample).Assembly);
            });
    }
} 