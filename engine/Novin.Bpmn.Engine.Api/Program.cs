using Microsoft.AspNetCore.SignalR;
using Novin.Bpmn.Engine.Application;
using Novin.Bpmn.Engine.Application.Hubs;
using Novin.Bpmn.Engine.Infrastructure;
using Quartz;
using Novin.Bpmn.Engine.Application.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add SignalR for client communications with CORS support
// Configure SignalR to use Newtonsoft.Json to preserve types and avoid JsonElement issues
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true; // Enable detailed errors for debugging
}); 

// Add CORS policy to allow all origins (including SignalR)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("Content-Disposition"); // Allow SignalR headers
    });
});

// Add health checks
builder.Services.AddHealthChecks();

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

// Register test scenario runner for API access
builder.Services.AddScoped<Novin.Bpmn.Engine.Api.TestScenarios.TestScenarioRunner>();

// Register client registry for SignalR client management
builder.Services.AddSingleton<IClientRegistry, Novin.Bpmn.Engine.Api.Services.ClientRegistry>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseRouting();

// Enable CORS with "AllowAll" policy (must be after UseRouting but before UseAuthorization)
app.UseCors("AllowAll");

app.UseAuthorization();

// Map health check endpoints
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => true
});
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false // No specific checks for liveness
});

// Map SignalR hubs (must be after UseCors)
app.MapHub<ClientHub>("/bpmn/clientHub");

app.MapControllers();

app.Run();

