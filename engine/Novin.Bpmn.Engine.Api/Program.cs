using Novin.Bpmn.Engine.Application;
using Novin.Bpmn.Engine.Infrastructure;
using Novin.Bpmn.Engine.Api.Controllers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add BPMN Engine services
builder.Services.AddApplication();
builder.Services.AddInfrastructure();

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

// Demo: Test BPMN Engine
await RunDemoAsync(app.Services);

app.Run();

static async Task RunDemoAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("=== BPMN Engine Demo Started ===");

        // 1. Deploy a process
        logger.LogInformation("1. Deploying process...");
        var bpmnXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL"">
  <bpmn:process id=""demo-process"" name=""Demo Process"">
    <bpmn:startEvent id=""start"" />
    <bpmn:task id=""task1"" name=""Task 1"" />
    <bpmn:endEvent id=""end"" />
  </bpmn:process>
</bpmn:definitions>";

        var deployCommand = new Novin.Bpmn.Engine.Application.Commands.DeployProcess.DeployProcessCommand(
            "demo-process-key",
            bpmnXml,
            "Demo Process Deployment");
        
        var deployResult = await mediator.Send(deployCommand);
        
        logger.LogInformation("   ✓ Process deployed. DeploymentId: {DeploymentId}, Version: {Version}", 
            deployResult.DeploymentId, deployResult.Version);

        // 2. Start a process instance
        logger.LogInformation("2. Starting process instance...");
        var startCommand = new Novin.Bpmn.Engine.Application.Commands.StartProcess.StartProcessCommand(
            "demo-process-key",
            "Demo Process Instance",
            new Dictionary<string, object> { { "amount", 1000 } });
        
        var startResult = await mediator.Send(startCommand);
        
        logger.LogInformation("   ✓ Process started. ProcessId: {ProcessId}", startResult.ProcessId);

       


    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error in demo");
    }
}

