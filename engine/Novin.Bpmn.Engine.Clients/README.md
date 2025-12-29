# BPMN Engine Clients Library

This library provides client-side functionality for connecting to and communicating with BPMN engines via SignalR.

## Features

- **Auto-Connect Background Service**: Automatically connects to BPMN engine on application startup
- **SignalR-based Communication**: Real-time bidirectional communication with BPMN engines
- **Service Worker Discovery**: Automatically discovers and logs all registered workers on startup
- **Automatic Reconnection**: Built-in connection management with automatic reconnection
- **Client Registration**: Automatic registration with the engine upon connection
- **Work Item Processing**: Process work items received from the engine
- **Connection Monitoring**: Continuous monitoring and reconnection when connection is lost
- **Health Monitoring**: Connection status and health monitoring

## Installation

Add the package reference to your project:

```xml
<PackageReference Include="Novin.Bpmn.Engine.Clients" Version="1.0.0" />
```

## Quick Start

### 1. Configure Services

```csharp
using Novin.Bpmn.Engine.Clients.Extensions;
using Novin.Bpmn.Engine.Clients.Abstractions;

var builder = WebApplication.CreateBuilder(args);

// Configure BPMN engine client
builder.Services.AddBpmnEngineClient(
    clientId: "my-client-app",
    engineBaseUrl: "https://my-bpmn-engine.com",
    options => {
        options.EnableDetailedLogging = true;
        options.MaxConcurrentWorkItems = 5;
    });

// Register service workers
builder.Services.AddServiceWorker<MyServiceHandler>("email-worker", new[] { "SendEmail", "SendNotification" });
builder.Services.AddServiceWorker<DocumentProcessor>("doc-worker");

var app = builder.Build();

// Map client endpoints
app.MapBpmnEngineEndpoints();

// Connect to the engine
app.MapGet("/connect", async (IClientConnectionManager connectionManager) => {
    await connectionManager.RegisterWithEngineAsync();
    return "Connected to BPMN engine";
});

app.Run();
```

### 2. Create Service Handlers

```csharp
using Novin.Bpmn.Engine.Clients.Abstractions;

public class MyServiceHandler : BpmnServiceWorkerHandler
{
    public override string HandlerId => "MyServiceHandler";
    public override string WorkType => "SendEmail";

    public override async Task ExecuteAsync(WorkItem workItem, CancellationToken cancellationToken = default)
    {
        // Extract data from work item
        var email = workItem.Payload["email"].ToString();
        var subject = workItem.Payload["subject"].ToString();
        var body = workItem.Payload["body"].ToString();

        // Process the work
        await SendEmailAsync(email, subject, body);

        // Work is automatically completed when this method returns
    }

    private async Task SendEmailAsync(string email, string subject, string body)
    {
        // Your email sending logic here
        await Task.Delay(100); // Simulate work
    }
}
```

### 3. BPMN Service Task Configuration

To route service tasks to your client, add extension elements to your BPMN service tasks:

```xml
<bpmn:serviceTask id="sendEmail" name="Send Email">
  <bpmn:extensionElements>
    <bpmn:service clientId="my-client-app" />
  </bpmn:extensionElements>
</bpmn:serviceTask>
```

- `clientId`: (Optional) Specific client to route to. If not specified, broadcasts to all connected clients.

## API Endpoints

The library provides the following minimal API endpoints:

- `GET /bpmn/client/info` - Get client information
- `GET /bpmn/client/health` - Get client health status
- `GET /bpmn/workers` - Get registered service workers
- `GET /bpmn/workers/{workerId}` - Get specific worker details
- `POST /bpmn/connect` - Connect to BPMN engine
- `POST /bpmn/disconnect` - Disconnect from BPMN engine
- `GET /bpmn/connection/status` - Get connection status

## Advanced Configuration

### Custom Options

```csharp
builder.Services.AddBpmnEngineClient(
    clientId: "advanced-client",
    engineBaseUrl: "https://engine.example.com",
    options => {
        options.EnableDetailedLogging = true;
        options.MaxConcurrentWorkItems = 10;
        options.ConnectionTimeoutSeconds = 60;
        options.RetryPolicy.MaxRetries = 5;
        options.HealthCheck.Enabled = true;
        options.HealthCheck.IntervalSeconds = 30;
    });
```

### Multiple Service Workers

```csharp
// Register multiple workers with different capabilities
builder.Services.AddServiceWorker<EmailHandler>("email-worker", new[] { "SendEmail", "SendSMS" });
builder.Services.AddServiceWorker<DocumentHandler>("doc-worker", new[] { "ProcessPDF", "GenerateReport" });
builder.Services.AddServiceWorker<ExternalApiHandler>("api-worker", new[] { "CallExternalAPI", "Webhook" });
```

## Error Handling

The library includes comprehensive error handling:

- **Connection Failures**: Automatic reconnection with exponential backoff
- **Work Processing Errors**: Failed work items are reported back to the engine
- **Handler Exceptions**: Exceptions in handlers are caught and reported

## Monitoring

Monitor your client using the built-in endpoints:

```bash
# Get client info
curl http://localhost:5000/bpmn/client/info

# Get health status
curl http://localhost:5000/bpmn/client/health

# Get connection status
curl http://localhost:5000/bpmn/connection/status
```

## Security Considerations

- Implement proper authentication for SignalR connections
- Validate work item data before processing
- Use HTTPS for production deployments
- Implement rate limiting for work processing
- Log sensitive operations appropriately

## Troubleshooting

### Common Issues

1. **Connection Refused**: Ensure the BPMN engine is running and accessible
2. **No Workers Available**: Check that service workers are properly registered
3. **Work Not Processing**: Verify work types match between handlers and BPMN tasks

### Debugging

Enable detailed logging to troubleshoot issues:

```csharp
builder.Services.AddBpmnEngineClient(
    clientId: "debug-client",
    engineBaseUrl: "https://engine.example.com",
    options => {
        options.EnableDetailedLogging = true;
    });
```

## Contributing

Contributions are welcome! Please ensure all changes include appropriate tests and documentation updates.