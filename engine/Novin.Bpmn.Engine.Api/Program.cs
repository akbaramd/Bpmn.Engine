using Novin.Bpmn.Engine.Application;
using Novin.Bpmn.Engine.Infrastructure;
using Novin.Bpmn.Engine.Api.Controllers;
using Quartz;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Api.TestScenarios;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure Quartz for Boundary Timer Scheduling
builder.Services.AddQuartz(q =>
{
    // Use a simple name scheduler
    q.UseSimpleTypeLoader();
    q.UseInMemoryStore();
    q.UseDefaultThreadPool(tp =>
    {
        tp.MaxConcurrency = 10;
    });
});

// Add Quartz hosted service
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

// Add BPMN Engine services
builder.Services.AddApplication();
builder.Services.AddInfrastructure();

// Override NullBoundaryTimerScheduler with QuartzBoundaryTimerScheduler
var existingService = builder.Services.FirstOrDefault(s => s.ServiceType == typeof(IBoundaryTimerScheduler));
if (existingService != null)
{
    builder.Services.Remove(existingService);
}
builder.Services.AddScoped<IBoundaryTimerScheduler, QuartzBoundaryTimerScheduler>();

// Register test scenario runner
builder.Services.AddScoped<TestScenarioRunner>();

var app = builder.Build();


// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

        // Run test scenarios
await RunTestScenariosAsync(app.Services, args);

static async Task RunTestScenariosAsync(IServiceProvider services, string[] args)
{
    using var scope = services.CreateScope();
    var runner = scope.ServiceProvider.GetRequiredService<TestScenarioRunner>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("=== BPMN Engine Test Scenarios ===");
        logger.LogInformation("");

        var allScenarios = TestScenarioRunner.GetAllScenarios();
        string? scenarioName = null;

        // Check command line arguments first
        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
        {
            scenarioName = args[0];
        }
        else
        {
            // Interactive prompt for scenario selection
            logger.LogInformation("Available test scenarios:");
            logger.LogInformation("");
            for (int i = 0; i < allScenarios.Count; i++)
            {
                logger.LogInformation(
                    "  {Index}. {Name} - {Description}",
                    i + 1,
                    allScenarios[i].Name,
                    allScenarios[i].Description);
            }
            logger.LogInformation("");
            logger.LogInformation("Options:");
            logger.LogInformation("  - Enter a number (1-{Count}) to run a specific scenario", allScenarios.Count);
            logger.LogInformation("  - Enter scenario name to run a specific scenario");
            logger.LogInformation("  - Press Enter to run all scenarios");
            logger.LogInformation("");
            Console.Write("Select scenario (or press Enter for all): ");

            var input = Console.ReadLine()?.Trim();
            
            if (string.IsNullOrWhiteSpace(input))
            {
                // Run all scenarios
                logger.LogInformation("Running all scenarios...");
                logger.LogInformation("");
                foreach (var scenario in allScenarios)
                {
                    await runner.RunScenarioAsync(scenario);
                }
                logger.LogInformation("=== All Test Scenarios Completed ===");
                return;
            }

            // Check if input is a number
            if (int.TryParse(input, out int selectedIndex) && selectedIndex >= 1 && selectedIndex <= allScenarios.Count)
            {
                scenarioName = allScenarios[selectedIndex - 1].Name;
            }
            else
            {
                // Treat as scenario name
                scenarioName = input;
            }
        }

        // Run selected scenario
        if (!string.IsNullOrWhiteSpace(scenarioName))
        {
            var scenario = TestScenarioRunner.FindScenario(scenarioName);
            if (scenario == null)
            {
                logger.LogError("Scenario not found: {ScenarioName}", scenarioName);
                logger.LogInformation("Available scenarios:");
                foreach (var s in allScenarios)
                {
                    logger.LogInformation("  - {Name}", s.Name);
                }
                return;
            }

            await runner.RunScenarioAsync(scenario);
            logger.LogInformation("=== Test Scenario Completed ===");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error running test scenarios");
        throw;
    }
}

