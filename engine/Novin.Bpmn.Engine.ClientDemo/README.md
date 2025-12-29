# BPMN Engine Client Demo

This project demonstrates how to create a client application that connects to and communicates with the BPMN Engine server using the `Novin.Bpmn.Engine.Clients` library.

## Overview

The client demo showcases:

- **SignalR-based communication** with the BPMN Engine server
- **Service worker registration** for handling different types of work
- **Automatic connection management** with reconnection support
- **Work item processing** from BPMN service tasks
- **Real-time bidirectional communication**

## Features

### Service Handlers

The demo includes three sample service handlers:

1. **EmailServiceHandler** - Handles email sending operations
   - Work Types: `SendEmail`, `SendNotification`
   - Simulates sending emails with configurable recipients, subjects, and bodies

2. **DocumentProcessorHandler** - Handles document processing operations
   - Work Types: `ProcessDocument`, `GenerateReport`
   - Simulates document processing and report generation

3. **ExternalApiHandler** - Handles external API calls
   - Work Types: `CallExternalApi`, `SendWebhook`
   - Makes HTTP requests to external services

## Prerequisites

- .NET 8.0 SDK
- BPMN Engine server running on `http://localhost:5000`
- The client demo project must be able to connect to the BPMN Engine's SignalR hub

## Configuration

The client is configured in `Program.cs`:

```csharp
builder.Services.AddBpmnEngineClient(
    clientId: "bpmn-client-demo",
    engineBaseUrl: "http://localhost:5000",
    options =>
    {
        options.EnableDetailedLogging = true;
        options.MaxConcurrentWorkItems = 5;
        options.ConnectionTimeoutSeconds = 30;
    });
```

### Configuration Options

- **ClientId**: Unique identifier for this client
- **EngineBaseUrl**: URL of the BPMN Engine server
- **EnableDetailedLogging**: Enable verbose logging
- **MaxConcurrentWorkItems**: Maximum concurrent work processing
- **ConnectionTimeoutSeconds**: SignalR connection timeout

## Running the Demo

1. **Start the BPMN Engine server** (ensure it's running on `http://localhost:5000`)

2. **Start the client demo**:
   ```bash
   cd Novin.Bpmn.Engine.ClientDemo
   dotnet run
   ```

3. **Initialize the demo**:
   ```bash
   curl http://localhost:5001/demo/test-email
   ```

4. **Connect to the engine**:
   ```bash
   curl http://localhost:5001/demo/connect
   ```

## API Endpoints

### BPMN Client Endpoints (from library)

- `GET /bpmn/client/info` - Client information
- `GET /bpmn/client/health` - Client health status
- `GET /bpmn/workers` - Registered service workers
- `POST /bpmn/connect` - Connect to BPMN engine
- `POST /bpmn/disconnect` - Disconnect from BPMN engine
- `GET /bpmn/connection/status` - Connection status

### Demo-Specific Endpoints

- `GET /demo/test-email` - Initialize demo and show status
- `GET /demo/connect` - Connect to BPMN engine
- `GET /demo/disconnect` - Disconnect from BPMN engine
- `GET /demo/status` - Get client status

## BPMN Process Integration

To use this client with BPMN processes, create service tasks with extension elements:

```xml
<bpmn:serviceTask id="sendEmailTask" name="Send Email">
  <bpmn:extensionElements>
    <bpmn:service clientId="bpmn-client-demo" />
  </bpmn:extensionElements>
</bpmn:serviceTask>
```

### Extension Element Format

```xml
<bpmn:extensionElements>
  <bpmn:service clientId="client-id" />
</bpmn:extensionElements>
```

- **clientId**: (Optional) Specific client to route the work to
- If not specified, work is broadcast to all connected clients

## Testing the Integration

1. **Deploy a BPMN process** with service tasks that use the client
2. **Start a process instance** through the BPMN Engine API
3. **Monitor the client logs** to see work items being processed
4. **Check the process status** to verify completion

## Work Item Payload

Service handlers receive work items with the following structure:

```json
{
  "id": "work-item-guid",
  "workType": "SendEmail",
  "payload": {
    "to": "user@example.com",
    "subject": "Hello",
    "body": "Message content"
  },
  "metadata": {
    "processId": "process-guid",
    "elementId": "service-task-id"
  },
  "priority": 0,
  "createdAt": "2025-12-26T14:30:00Z"
}
```

## Monitoring and Debugging

### Logs

The client provides detailed logging for:

- Connection events
- Work item processing
- Error handling
- Service handler execution

### Health Checks

Monitor client health via:

```bash
curl http://localhost:5001/bpmn/client/health
```

### Connection Status

Check connection status:

```bash
curl http://localhost:5001/bpmn/connection/status
```

## Scaling and Production

For production deployments:

1. **Use HTTPS** for secure communication
2. **Implement authentication** for SignalR connections
3. **Configure proper logging** and monitoring
4. **Set appropriate timeouts** and retry policies
5. **Use load balancing** for multiple client instances
6. **Implement proper error handling** and circuit breakers

## Troubleshooting

### Common Issues

1. **Connection Refused**: Ensure BPMN Engine is running on port 5000
2. **Work Not Processed**: Check that work types match between BPMN tasks and handlers
3. **Client Not Registered**: Verify client connection and registration
4. **Timeout Errors**: Adjust timeout settings in configuration

### Debug Mode

Enable detailed logging by setting:

```csharp
options.EnableDetailedLogging = true;
```

This will log all SignalR communication, work processing, and internal operations.