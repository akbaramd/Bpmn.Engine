using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing;
using Novin.Bpmn.EventSourcing.Core.Executions;
using Novin.Bpmn.EventSourcing.Core.Process;

class Program
{
    static async Task Main(string[] args)
    {
        using IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            })
            .ConfigureServices((context, services) =>
            {
                services.AddBpmnEngine();

                // در صورت نیاز سرویس‌های دیگر یا Mock اضافه کنید
            })
            .Build();

        // شروع Host (سرویس‌های پس‌زمینه فعال می‌شوند)
        await host.StartAsync();

        // نمونه مدل BPMN را بارگذاری و دیپلوی می‌کنیم
        var filePath = Path.Combine(AppContext.BaseDirectory, "Bpmn", "diagram_2.bpmn");
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"File not found: {filePath}");
            return;
        }

        var bpmnXml = await File.ReadAllTextAsync(filePath);

        var deploymentService = host.Services.GetRequiredService<IDeploymentService>();
        var deploymentKey = "ConsoleAppTestDeployment";

        var deployment = deploymentService.Deploy(deploymentKey, bpmnXml);

        Console.WriteLine($"Deployed process with key: {deployment.DeploymentKey}, ProcessId: {deployment.DeploymentKey}");

        var processEngine = host.Services.GetRequiredService<IProcessEngine>();
        var instanceId = Guid.NewGuid();

        // شروع اجرای پروسس
        await processEngine.StartProcessAsync(deployment.DeploymentKey, "start_process");

        Console.WriteLine($"Process started with InstanceId: {instanceId}");

        // بررسی وضعیت اجرای پروسس
        var executionRepo = host.Services.GetRequiredService<IExecutionContextRepository>();

        bool completed = false;
        for (int i = 0; i < 60; i++)  // حداکثر 30 ثانیه انتظار
        {
            var contexts = executionRepo.GetByInstanceId(instanceId);
            if (contexts.All(c => c.State == ExecutionState.Completed))
            {
                completed = true;
                break;
            }

            Console.WriteLine("Process not completed yet... waiting 500ms");
            await Task.Delay(500);
        }

        Console.WriteLine(completed
            ? "Process completed successfully!"
            : "Process did not complete in expected time.");

        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();

        // توقف Host و سرویس‌ها
        await host.StopAsync();
    }
}
