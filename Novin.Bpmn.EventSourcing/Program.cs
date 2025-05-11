using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core;
using Novin.Bpmn.EventSourcing.Examples;
using Novin.Bpmn.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace Novin.Bpmn.EventSourcingApp;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            
            // نمایش منوی گزینه‌ها
            if (args.Length == 0)
            {
                ShowMenu();
                var choice = Console.ReadLine()?.Trim().ToLower();
                return await RunExample(choice);
            }
            else
            {
                return await RunExample(args[0].ToLower());
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"خطای کلی: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }
    
    private static void ShowMenu()
    {
        Console.WriteLine("لطفاً مثال مورد نظر را انتخاب کنید:");
        Console.WriteLine("2. مثال وظایف کاربری");
        Console.WriteLine("3. ابزار تشخیص ثبت هندلرها");
        Console.WriteLine("4. مثال مخزن تعاریف BPMN");
        Console.WriteLine("5. مثال ذخیره‌سازی حافظه‌ای تعاریف BPMN");
        Console.WriteLine("q. خروج");
        Console.Write("انتخاب شما: ");
    }
    
    private static async Task<int> RunExample(string? choice)
    {
        switch (choice)
        {
            case "1":
                await InclusiveGatewayExample.RunAsync();
                break;
            
            case "2":
            case "usertask":
            case "task":
                await UserTaskExample.RunAsync();
                break;
                
            case "3":
            case "diagnostics":
            case "handlers":
                await HandlerDiagnosticsTool.RunAsync();
                break;
                
            case "4":
            case "definition":
            case "store":
                await BpmnDefinitionStoreExample.RunAsync();
                break;
                
            case "5":
            case "storage":
            case "memory":
                await BpmnStorageExample.RunAsync();
                break;
                
            case "q":
            case "exit":
            case "quit":
                Console.WriteLine("خروج از برنامه");
                return 0;
                
            default:
                Console.WriteLine("گزینه نامعتبر. لطفاً دوباره امتحان کنید.");
                return 1;
        }
        
        return 0;
    }
    
    static async Task RunHostedServiceExample()
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Debug);
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddBpmnEventSourcing();
                
                // Explicitly register event handlers from the current assembly
                services.AddBpmnEventHandlers(typeof(Program).Assembly);
            })
            .Build();

        await host.RunAsync();
    }
} 