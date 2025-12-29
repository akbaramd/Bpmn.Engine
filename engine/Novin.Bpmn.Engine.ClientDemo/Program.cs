using Novin.Bpmn.Engine.ClientDemo.Handlers;
using Novin.Bpmn.Engine.Clients.Extensions;
using Novin.Bpmn.Engine.Clients.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure BPMN Engine Client
builder.Services.AddBpmnEngineClient(
    clientId: "bpmn-client-demo",
    engineBaseUrl: "http://localhost:5000", // BPMN Engine server address
    options =>
    {
        options.EnableDetailedLogging = true;
        options.MaxConcurrentWorkItems = 5;
        options.ConnectionTimeoutSeconds = 30;
    });

// Register service handlers
builder.Services.AddServiceWorker<MathHandler>(new[] { "CalculateSum" });
builder.Services.AddServiceWorker<SumUserTaskHandler>(new[] { "CalculateSum" });

// Add HTTP client for external API calls
builder.Services.AddHttpClient("ExternalApiClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Map BPMN Engine client endpoints
app.MapBpmnEngineEndpoints();


app.Run();
