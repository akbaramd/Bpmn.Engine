using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Novin.Bpmn.EventSourcing;

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
            })
            .Build();

        await host.RunAsync();
    }
} 