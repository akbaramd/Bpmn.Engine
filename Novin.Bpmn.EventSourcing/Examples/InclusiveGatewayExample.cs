using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core;
using Novin.Bpmn.EventSourcing.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing.Examples;

/// <summary>
/// Example of BPMN Inclusive Gateway Patterns with mathematical operations
/// Demonstrates how inclusive gateways evaluate conditions and perform calculations
/// based on different operators (+, -, *, /)
/// </summary>
public class InclusiveGatewayExample
{
    // BPMN XML with an inclusive gateway that forks based on operators
    private const string InclusiveGatewayXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<definitions xmlns=""http://www.omg.org/spec/BPMN/20100524/MODEL"" 
             xmlns:bpmndi=""http://www.omg.org/spec/BPMN/20100524/DI"" 
             xmlns:dc=""http://www.omg.org/spec/DD/20100524/DC"" 
             xmlns:di=""http://www.omg.org/spec/DD/20100524/DI""
             xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
             id=""Definitions_1"" 
             targetNamespace=""http://bpmn.io/schema/bpmn"">
  <process id=""MathOperationsProcess"" name=""Math Operations Process"" isExecutable=""true"">
    <!-- Start of process -->
    <startEvent id=""StartEvent_1"" name=""Start"">
      <outgoing>Flow_Start_Gateway</outgoing>
    </startEvent>
    <sequenceFlow id=""Flow_Start_Gateway"" sourceRef=""StartEvent_1"" targetRef=""InclusiveGateway_Fork"" />
    
    <!-- Inclusive Gateway Fork -->
    <inclusiveGateway id=""InclusiveGateway_Fork"" name=""Route by Operator"">
      <incoming>Flow_Start_Gateway</incoming>
      <outgoing>Flow_Addition</outgoing>
      <outgoing>Flow_Subtraction</outgoing>
      <outgoing>Flow_Multiplication</outgoing>
      <outgoing>Flow_Division</outgoing>
    </inclusiveGateway>
    
    <!-- Addition Path -->
    <sequenceFlow id=""Flow_Addition"" name=""Addition"" sourceRef=""InclusiveGateway_Fork"" targetRef=""ScriptTask_Addition"">
      <conditionExpression xsi:type=""tFormalExpression"">Variables[""operator""].Equals(""+"") || Variables[""operator""].Equals(""add"")</conditionExpression>
    </sequenceFlow>
    <scriptTask id=""ScriptTask_Addition"" name=""Perform Addition"">
      <incoming>Flow_Addition</incoming>
      <outgoing>Flow_Addition_Merge</outgoing>
      <script>
        <![CDATA[
        // Get input numbers
        var num1 = Convert.ToDouble(Variables[""num1""]);
        var num2 = Convert.ToDouble(Variables[""num2""]);
        
        // Calculate result
        var result = num1 + num2;
        
        // Store result and operation info
        Variables[""result_addition""] = result;
        Variables[""operation_addition""] = $""{num1} + {num2} = {result}"";
        Variables[""final_value_addition""] = result;
        
        Log($""Addition performed: {num1} + {num2} = {result}"");
        ]]>
      </script>
    </scriptTask>
    <sequenceFlow id=""Flow_Addition_Merge"" sourceRef=""ScriptTask_Addition"" targetRef=""InclusiveGateway_Merge"" />
    
    <!-- Subtraction Path -->
    <sequenceFlow id=""Flow_Subtraction"" name=""Subtraction"" sourceRef=""InclusiveGateway_Fork"" targetRef=""ScriptTask_Subtraction"">
      <conditionExpression xsi:type=""tFormalExpression"">Variables[""operator""].Equals(""-"") || Variables[""operator""].Equals(""subtract"")</conditionExpression>
    </sequenceFlow>
    <scriptTask id=""ScriptTask_Subtraction"" name=""Perform Subtraction"">
      <incoming>Flow_Subtraction</incoming>
      <outgoing>Flow_Subtraction_Merge</outgoing>
      <script>
        <![CDATA[
        // Get input numbers
        var num1 = Convert.ToDouble(Variables[""num1""]);
        var num2 = Convert.ToDouble(Variables[""num2""]);
        
        // Calculate result
        var result = num1 - num2;
        
        // Store result and operation info
        Variables[""result_subtraction""] = result;
        Variables[""operation_subtraction""] = $""{num1} - {num2} = {result}"";
        Variables[""final_value_subtraction""] = result;
        
        Log($""Subtraction performed: {num1} - {num2} = {result}"");
        ]]>
      </script>
    </scriptTask>
    <sequenceFlow id=""Flow_Subtraction_Merge"" sourceRef=""ScriptTask_Subtraction"" targetRef=""InclusiveGateway_Merge"" />
    
    <!-- Multiplication Path -->
    <sequenceFlow id=""Flow_Multiplication"" name=""Multiplication"" sourceRef=""InclusiveGateway_Fork"" targetRef=""ScriptTask_Multiplication"">
      <conditionExpression xsi:type=""tFormalExpression"">Variables[""operator""].Equals(""*"") || Variables[""operator""].Equals(""multiply"")</conditionExpression>
    </sequenceFlow>
    <scriptTask id=""ScriptTask_Multiplication"" name=""Perform Multiplication"">
      <incoming>Flow_Multiplication</incoming>
      <outgoing>Flow_Multiplication_Merge</outgoing>
      <script>
        <![CDATA[
        // Get input numbers
        var num1 = Convert.ToDouble(Variables[""num1""]);
        var num2 = Convert.ToDouble(Variables[""num2""]);
        
        // Calculate result
        var result = num1 * num2;
        
        // Store result and operation info
        Variables[""result_multiplication""] = result;
        Variables[""operation_multiplication""] = $""{num1} * {num2} = {result}"";
        Variables[""final_value_multiplication""] = result;
        
        Log($""Multiplication performed: {num1} * {num2} = {result}"");
        ]]>
      </script>
    </scriptTask>
    <sequenceFlow id=""Flow_Multiplication_Merge"" sourceRef=""ScriptTask_Multiplication"" targetRef=""InclusiveGateway_Merge"" />
    
    <!-- Division Path -->
    <sequenceFlow id=""Flow_Division"" name=""Division"" sourceRef=""InclusiveGateway_Fork"" targetRef=""ScriptTask_Division"">
      <conditionExpression xsi:type=""tFormalExpression"">Variables[""operator""].Equals(""/"") || Variables[""operator""].Equals(""divide"")</conditionExpression>
    </sequenceFlow>
    <scriptTask id=""ScriptTask_Division"" name=""Perform Division"">
      <incoming>Flow_Division</incoming>
      <outgoing>Flow_Division_Merge</outgoing>
      <script>
        <![CDATA[
        // Get input numbers
        var num1 = Convert.ToDouble(Variables[""num1""]);
        var num2 = Convert.ToDouble(Variables[""num2""]);
        
        // Check for division by zero
        if (num2 == 0) {
            Variables[""result_division""] = ""Error: Division by zero"";
            Variables[""operation_division""] = $""{num1} / {num2} = Error: Division by zero"";
            Variables[""final_value_division""] = null;
            Log(""Division error: Cannot divide by zero"");
        } else {
            // Calculate result
            var result = num1 / num2;
            
            // Store result and operation info
            Variables[""result_division""] = result;
            Variables[""operation_division""] = $""{num1} / {num2} = {result}"";
            Variables[""final_value_division""] = result;
            
            Log($""Division performed: {num1} / {num2} = {result}"");
        }
        ]]>
      </script>
    </scriptTask>
    <sequenceFlow id=""Flow_Division_Merge"" sourceRef=""ScriptTask_Division"" targetRef=""InclusiveGateway_Merge"" />
    
    <!-- Inclusive Gateway Merge -->
    <inclusiveGateway id=""InclusiveGateway_Merge"" name=""Merge Results"">
      <incoming>Flow_Addition_Merge</incoming>
      <incoming>Flow_Subtraction_Merge</incoming>
      <incoming>Flow_Multiplication_Merge</incoming>
      <incoming>Flow_Division_Merge</incoming>
      <outgoing>Flow_Merge_Summary</outgoing>
    </inclusiveGateway>
    <sequenceFlow id=""Flow_Merge_Summary"" sourceRef=""InclusiveGateway_Merge"" targetRef=""ScriptTask_Summary"" />
    
    <!-- Summarize results -->
    <scriptTask id=""ScriptTask_Summary"" name=""Summarize Results"">
      <incoming>Flow_Merge_Summary</incoming>
      <outgoing>Flow_Summary_End</outgoing>
      <script>
        <![CDATA[
        // Create a summary of all operations performed
        var summary = ""Math Operations Summary:\n"";
        var finalValues = new Dictionary<string, object>();
        
        if (Variables.ContainsKey(""operation_addition"")) {
            summary += Variables[""operation_addition""] + ""\n"";
            finalValues[""addition""] = Variables[""final_value_addition""];
        }
        
        if (Variables.ContainsKey(""operation_subtraction"")) {
            summary += Variables[""operation_subtraction""] + ""\n"";
            finalValues[""subtraction""] = Variables[""final_value_subtraction""];
        }
        
        if (Variables.ContainsKey(""operation_multiplication"")) {
            summary += Variables[""operation_multiplication""] + ""\n"";
            finalValues[""multiplication""] = Variables[""final_value_multiplication""];
        }
        
        if (Variables.ContainsKey(""operation_division"")) {
            summary += Variables[""operation_division""] + ""\n"";
            finalValues[""division""] = Variables[""final_value_division""];
        }
        
        // Store the final summary and values
        Variables[""operations_summary""] = summary;
        Variables[""final_values""] = finalValues;
        
        // Log the summary
        Log(""Operations completed successfully"");
        Log(summary);
        Log($""Final values: {string.Join("", "", finalValues.Select(kv => $""{kv.Key}: {kv.Value}""))}"");
        ]]>
      </script>
    </scriptTask>
    <sequenceFlow id=""Flow_Summary_End"" sourceRef=""ScriptTask_Summary"" targetRef=""EndEvent_1"" />
    
    <!-- End of process -->
    <endEvent id=""EndEvent_1"" name=""End"">
      <incoming>Flow_Summary_End</incoming>
    </endEvent>
  </process>
</definitions>";

    private const string DeploymentKeyBase = "math-operations-example";
    private const int ProcessingDelay = 500;
    private const int MaxRetries = 3;
    private const int RetryDelay = 1000;
    private const int WaitBetweenExamples = 2000;

    /// <summary>
    /// Run the inclusive gateway example with mathematical operations
    /// </summary>
    public static async Task RunAsync()
    {
        IHost? host = null;
        try
        {
            // Create and start the host with required services
            host = CreateHostBuilder().Build();
            await host.StartAsync();
            
            var logger = host.Services.GetRequiredService<ILogger<InclusiveGatewayExample>>();
            var bpmnProcessor = host.Services.GetRequiredService<BpmnService>();
            var eventBus = host.Services.GetRequiredService<IEventBus>();
            var eventStore = host.Services.GetRequiredService<IEventStore>();

            logger.LogInformation("Starting Math Operations Example with Inclusive Gateway");

            // Run with addition operator
            await RunWithOperatorAsync(
                bpmnProcessor, 
                eventStore,
                new Dictionary<string, object> { { "num1", 10 }, { "num2", 5 }, { "operator", "+" } },
                "Run 1: Addition operation (10 + 5)",
                logger);
                
            // Wait between examples to avoid concurrency issues
        }
        catch (Exception ex)
        {
            if (host?.Services.GetService<ILogger<InclusiveGatewayExample>>() is ILogger<InclusiveGatewayExample> logger)
            {
                logger.LogError(ex, "Error executing math operations example");
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
        string uniqueKey,
        ILogger<InclusiveGatewayExample> logger)
    {
        var deploymentInfo = await bpmnProcessor.DeployProcessDefinitionAsync(
            uniqueKey, 
            InclusiveGatewayXml,
            $"Math Operations Example - {uniqueKey}");
            
        logger.LogInformation("Deployed process definition with ID {ProcessDefinitionId} and key {DeploymentKey}", 
            deploymentInfo.DefinitionId, uniqueKey);
            
        return deploymentInfo.DefinitionId;
    }

    private static async Task RunWithOperatorAsync(
        BpmnService bpmnProcessor,
        IEventStore eventStore,
        Dictionary<string, object> variables,
        string description,
        ILogger<InclusiveGatewayExample> logger)
    {
        logger.LogInformation("=== {Description} ===", description);
        
        // Create a unique deployment key for each run to avoid concurrency issues
        string uniqueDeploymentKey = $"{DeploymentKeyBase}-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        
        try
        {
            // Deploy a fresh definition for each run
            await DeployProcessDefinitionAsync(bpmnProcessor, uniqueDeploymentKey, logger);
            
            // Start process instance with the given variables
            var processInstanceId = await StartProcessInstanceWithRetryAsync(
                bpmnProcessor, uniqueDeploymentKey, variables, logger);
                
            logger.LogInformation("Created process instance with ID {ProcessInstanceId}", 
                processInstanceId);
                
            // Wait for process to complete
            bool isCompleted = false;
            int attempts = 0;
            const int maxAttempts = 30;
            
            while (!isCompleted && attempts < maxAttempts)
            {
                attempts++;
                
                // Check if process has completed
                var state = await bpmnProcessor.GetProcessInstanceStateAsync(processInstanceId);
                isCompleted = state.Status == ProcessStatus.Completed;
                
                if (!isCompleted)
                {
                    await Task.Delay(ProcessingDelay);
                }
            }
            
            
            // Check for process completion
            if (isCompleted)
            {
                // Get final process state to retrieve results
                var finalState = await bpmnProcessor.GetProcessInstanceStateAsync(processInstanceId);
                
                
            }
            else
            {
                logger.LogWarning("Process did not complete within the expected time");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running example for {Description}", description);
        }
        
        logger.LogInformation("=== End of {Description} ===\n", description);
    }

    private static async Task<string> StartProcessInstanceWithRetryAsync(
        BpmnService bpmnProcessor,
        string deploymentKey,
        Dictionary<string, object> variables,
        ILogger<InclusiveGatewayExample> logger)
    {
        int retryCount = 0;
        
        while (true)
        {
            try
            {
                return await bpmnProcessor.StartProcessInstanceAsync(
                    deploymentKey,
                    "MathOperationsProcess",
                    variables);
            }
            catch (Exception ex) when ((ex.Message.Contains("Concurrency conflict") || 
                                      ex.InnerException?.Message?.Contains("Concurrency conflict") == true) && 
                                      retryCount < MaxRetries)
            {
                retryCount++;
                logger.LogWarning("Concurrency conflict detected on attempt {RetryCount} of {MaxRetries}. Waiting before retry...", 
                    retryCount, MaxRetries);
                await Task.Delay(RetryDelay * retryCount * 2); // Increase delay with each retry
            }
            catch (Exception ex)
            {
                // Rethrow other exceptions after logging
                logger.LogError(ex, "Unhandled error starting process instance");
                throw;
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

                services.AddElasticsearch(options => {
                    options.Url = "http://localhost:9200";
                    options.Username = "elastic";
                    options.Password = "changeme";
                    options.EnableSsl = false;
                    options.VerifySsl = false;
                });
            });
    }
} 