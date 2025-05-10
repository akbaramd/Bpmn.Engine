using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core.Deployment;
using Novin.Bpmn.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Novin.Bpmn.EventSourcing.Examples
{
    /// <summary>
    /// مثال استفاده از سیستم دو لایه‌ی ذخیره‌سازی تعاریف BPMN
    /// </summary>
    public static class BpmnStorageExample
    {
        /// <summary>
        /// کلاس کمکی برای لاگر
        /// </summary>
        private class LoggerContext { }

        // نمونه ساده یک فرآیند BPMN
        private const string SimpleProcessXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
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
    <userTask id=""UserTask_1"" name=""Review Request"">
      <incoming>Flow_1</incoming>
      <outgoing>Flow_2</outgoing>
    </userTask>
    <sequenceFlow id=""Flow_2"" sourceRef=""UserTask_1"" targetRef=""EndEvent_1"" />
    <endEvent id=""EndEvent_1"" name=""End"">
      <incoming>Flow_2</incoming>
    </endEvent>
  </process>
</definitions>";

        // نمونه دوم یک فرآیند BPMN
        private const string AnotherProcessXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<definitions xmlns=""http://www.omg.org/spec/BPMN/20100524/MODEL"" 
             xmlns:bpmndi=""http://www.omg.org/spec/BPMN/20100524/DI"" 
             xmlns:dc=""http://www.omg.org/spec/DD/20100524/DC"" 
             xmlns:di=""http://www.omg.org/spec/DD/20100524/DI"" 
             id=""Definitions_2"" 
             targetNamespace=""http://bpmn.io/schema/bpmn"">
  <process id=""Process_2"" isExecutable=""true"">
    <startEvent id=""StartEvent_1"" name=""Start"">
      <outgoing>Flow_1</outgoing>
    </startEvent>
    <sequenceFlow id=""Flow_1"" sourceRef=""StartEvent_1"" targetRef=""ServiceTask_1"" />
    <serviceTask id=""ServiceTask_1"" name=""Process Data"">
      <incoming>Flow_1</incoming>
      <outgoing>Flow_2</outgoing>
    </serviceTask>
    <sequenceFlow id=""Flow_2"" sourceRef=""ServiceTask_1"" targetRef=""EndEvent_1"" />
    <endEvent id=""EndEvent_1"" name=""End"">
      <incoming>Flow_2</incoming>
    </endEvent>
  </process>
</definitions>";

        /// <summary>
        /// اجرای مثال ذخیره‌سازی تعاریف BPMN
        /// </summary>
        public static async Task RunAsync()
        {
            // ایجاد هاست با سرویس‌های مورد نیاز
            var host = CreateHostBuilder().Build();
            await host.StartAsync();
            
            var logger = host.Services.GetRequiredService<ILogger<LoggerContext>>();
            var definitionStore = host.Services.GetRequiredService<IBpmnDefinitionStore>();
            var definitionStorage = host.Services.GetRequiredService<IBpmnDefinitionStorage>();

            try
            {
                logger.LogInformation("Running BPMN Storage Example");
                
                // مقداردهی اولیه سیستم ذخیره‌سازی
                logger.LogInformation("Initializing storage system...");
                await definitionStore.InitializeAsync();
                await definitionStorage.InitializeAsync();
                
                logger.LogInformation("Starting state: {0} definitions in storage", definitionStorage.Count);
                
                // پارس کردن XML به مدل تعریف
                var serializer = new XmlSerializer(typeof(BpmnDefinitions));
                
                // تعریف فرآیند اول
                string deploymentKey1 = "simple-process";
                BpmnDefinitions definitions1;
                using (var reader = new StringReader(SimpleProcessXml))
                {
                    definitions1 = (BpmnDefinitions)serializer.Deserialize(reader);
                    
                    // ذخیره تعریف فرآیند در مخزن - این فقط در مخزن ذخیره می‌شود
                    await definitionStore.SaveDefinitionAsync(
                        deploymentKey1,
                        SimpleProcessXml,
                        definitions1,
                        "Simple Process Example");
                }
                
                // بررسی وجود در مخزن و عدم وجود در حافظه
                var storeInfo = await definitionStore.GetDeploymentInfoAsync(deploymentKey1);
                logger.LogInformation("Definition stored in persistent store: {0}", storeInfo != null ? "Yes" : "No");
                
                // افزودن دستی به حافظه (معمولاً توسط BpmnDefinitionStorageInitializer انجام می‌شود)
                definitionStorage.AddDefinition(deploymentKey1, storeInfo, definitions1);
                logger.LogInformation("Manually added to in-memory storage. Storage now has {0} definitions", definitionStorage.Count);
                
                // دریافت از ذخیره‌سازی حافظه
                var cachedInfo = definitionStorage.GetDeploymentInfo(deploymentKey1);
                var cachedDefinition = definitionStorage.GetParsedDefinition(deploymentKey1);
                
                logger.LogInformation("Retrieved from memory cache: ID={0}, Process ID={1}",
                    cachedInfo.DefinitionId,
                    cachedDefinition.Items?.OfType<BpmnProcess>().FirstOrDefault()?.id ?? "<none>");
                
                // تعریف فرآیند دوم - اینبار فقط در مخزن
                string deploymentKey2 = "another-process";
                BpmnDefinitions definitions2;
                using (var reader = new StringReader(AnotherProcessXml))
                {
                    definitions2 = (BpmnDefinitions)serializer.Deserialize(reader);
                    
                    // ذخیره تعریف فرآیند در مخزن
                    await definitionStore.SaveDefinitionAsync(
                        deploymentKey2,
                        AnotherProcessXml,
                        definitions2,
                        "Another Process Example");
                }
                
                // در حالت واقعی، BpmnProcessorService چک می‌کند اگر تعریف در حافظه نباشد، از مخزن بازیابی می‌کند
                var deploymentInfo2 = definitionStorage.GetDeploymentInfo(deploymentKey2);
                if (deploymentInfo2 == null)
                {
                    logger.LogInformation("Second definition not found in memory storage, loading from store...");
                    var storeInfo2 = await definitionStore.GetDeploymentInfoAsync(deploymentKey2);
                    if (storeInfo2 != null)
                    {
                        definitionStorage.AddDefinition(deploymentKey2, storeInfo2, definitions2);
                        logger.LogInformation("Added second definition to memory. Storage now has {0} definitions", 
                            definitionStorage.Count);
                    }
                }
                
                // جستجو بر اساس شناسه فرآیند
                var processWith1 = definitionStorage.FindDeploymentsByProcessId("Process_1");
                var processWith2 = definitionStorage.FindDeploymentsByProcessId("Process_2");
                
                logger.LogInformation("Found {0} deployments with Process_1, {1} deployments with Process_2",
                    processWith1.Count, processWith2.Count);
                
                // دریافت تمام کلیدهای نصب
                var allKeys = definitionStorage.GetAllDeploymentKeys();
                logger.LogInformation("All deployment keys: {0}", string.Join(", ", allKeys));
                
                // حذف یک تعریف از مخزن - باید از حافظه هم حذف شود
                await definitionStore.DeleteDefinitionAsync(deploymentKey1);
                
                // حذف دستی از حافظه (معمولاً با استفاده از event یا هماهنگی انجام می‌شود)
                definitionStorage.RemoveDefinition(deploymentKey1);
                
                logger.LogInformation("Deleted one definition. Storage now has {0} definitions", definitionStorage.Count);
                
                // بررسی وجود تعریف
                bool exists1 = definitionStorage.HasDefinition(deploymentKey1);
                bool exists2 = definitionStorage.HasDefinition(deploymentKey2);
                
                logger.LogInformation("Definition {0} exists: {1}, Definition {2} exists: {3}",
                    deploymentKey1, exists1, deploymentKey2, exists2);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in BPMN Storage Example");
            }
            finally
            {
                await host.StopAsync();
            }
            
            logger.LogInformation("BPMN Storage Example completed");
        }
        
        /// <summary>
        /// ایجاد سازنده هاست با سرویس‌های مورد نیاز
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
                    // ثبت سرویس‌های Event Sourcing
                    services.AddBpmnEventSourcing(options =>
                    {
                        options.DefinitionsDirectory = Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory, "ExampleDefinitions");
                    });
                });
        }
    }
} 