using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Novin.Bpmn.EventSourcing.Examples
{
    /// <summary>
    /// مثال استفاده از مخزن تعاریف BPMN
    /// </summary>
    public static class BpmnDefinitionStoreExample
    {
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
    <sequenceFlow id=""Flow_1"" sourceRef=""StartEvent_1"" targetRef=""EndEvent_1"" />
    <endEvent id=""EndEvent_1"" name=""End"">
      <incoming>Flow_1</incoming>
    </endEvent>
  </process>
</definitions>";

        /// <summary>
        /// اجرای مثال مخزن تعاریف BPMN
        /// </summary>
        public static async Task RunAsync()
        {
            // ایجاد هاست با سرویس‌های مورد نیاز
            var host = CreateHostBuilder().Build();
            await host.StartAsync();
            
            var logger = host.Services.GetRequiredService<ILogger<UserTaskExample>>();
            var definitionStore = host.Services.GetRequiredService<IBpmnDefinitionStore>();

            try
            {
                logger.LogInformation("Running BPMN Definition Store Example");
                
                // پارس کردن XML به مدل تعریف
                var serializer = new XmlSerializer(typeof(BpmnDefinitions));
                using var reader = new StringReader(SimpleProcessXml);
                var definitions = (BpmnDefinitions)serializer.Deserialize(reader);
                
                // ذخیره تعریف فرآیند
                string deploymentKey = "simple-process";
                await definitionStore.SaveDefinitionAsync(
                    deploymentKey,
                    SimpleProcessXml,
                    definitions,
                    "Simple Process Example");
                    
                logger.LogInformation("Deployed simple process with key {DeploymentKey}", deploymentKey);
                
                // بازیابی تعریف از مخزن
                var deploymentInfo = await definitionStore.GetDeploymentInfoAsync(deploymentKey);
                logger.LogInformation("Retrieved deployment info: ID={DefinitionId}, Label={Label}, DeployTime={DeployTime}",
                    deploymentInfo.DefinitionId,
                    deploymentInfo.Label,
                    deploymentInfo.DeploymentTime);
                
                // بازیابی تعریف پارس شده
                var parsedDefinition = await definitionStore.GetParsedDefinitionAsync(
                    deploymentKey,
                    xml => 
                    {
                        using var xmlReader = new StringReader(xml);
                        return (BpmnDefinitions)serializer.Deserialize(xmlReader);
                    });
                    
                logger.LogInformation("Retrieved parsed definition: ID={DefinitionId}, Process Count={ProcessCount}",
                    parsedDefinition.id,
                    parsedDefinition.Items?.OfType<BpmnProcess>().Count() ?? 0);
                
                // دریافت تمام کلیدهای نصب
                var allKeys = await definitionStore.GetAllDeploymentKeysAsync();
                logger.LogInformation("All deployment keys: {Keys}", string.Join(", ", allKeys));
                
                // در پایان می‌توانیم تعریف را حذف کنیم
                // (غیرفعال برای نمایش در خروجی)
                // await definitionStore.DeleteDefinitionAsync(deploymentKey);
                // logger.LogInformation("Deleted deployment with key {DeploymentKey}", deploymentKey);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in BPMN Definition Store Example");
            }
            finally
            {
                await host.StopAsync();
            }
            
            logger.LogInformation("BPMN Definition Store Example completed");
        }
        
        /// <summary>
        /// ایجاد سازنده هاست با سرویس‌های مورد نیاز
        /// </summary>
        private static IHostBuilder CreateHostBuilder()
        {
            var definitionsPath = Path.Combine(Path.GetTempPath(), "BpmnDefinitions");
            
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
                        options.DefinitionsDirectory = definitionsPath;
                    });
                });
        }
    }
} 