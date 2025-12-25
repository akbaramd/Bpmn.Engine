using Novin.Bpmn.Engine.Application;
using Novin.Bpmn.Engine.Infrastructure;
using Quartz;
using Novin.Bpmn.Engine.Application.Services;

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

// Register test scenario runner for API access
builder.Services.AddScoped<Novin.Bpmn.Engine.Api.TestScenarios.TestScenarioRunner>();

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

app.Run();

