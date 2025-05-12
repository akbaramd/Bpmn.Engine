using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core;
using Novin.Bpmn.EventSourcing.Examples;
using Novin.Bpmn.Models;

namespace Novin.Bpmn.EventSourcingApp;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            
            // Display menu if no arguments provided
            if (args.Length == 0)
            {
                ShowMenu();
                var choice = Console.ReadLine()?.Trim().ToLower();
                return await RunExample(choice);
            }
            
            return await RunExample(args[0].ToLower());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }
    
    private static void ShowMenu()
    {
        Console.WriteLine("Please select an example:");
        Console.WriteLine("1. Inclusive Gateway");
        Console.WriteLine("q. Exit");
        Console.Write("Your choice: ");
    }
    
    private static async Task<int> RunExample(string? choice)
    {
        switch (choice)
        {
            case "1":
                await InclusiveGatewayExample.RunAsync();
                break;
            default:
                Console.WriteLine("Invalid option. Please try again.");
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
                services.AddBpmnEventSourcing();
                services.AddBpmnEventHandlers(typeof(Program).Assembly);
            })
            .Build();

        await host.RunAsync();
    }
} 