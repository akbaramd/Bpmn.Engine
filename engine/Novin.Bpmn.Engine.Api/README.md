# BPMN Engine API

A comprehensive REST API for managing BPMN processes with full CORS support.

## Features

- ✅ **Deployment Management**: Deploy, activate, deactivate BPMN processes
- ✅ **Process Execution**: Start, monitor, complete process instances
- ✅ **Execution Flow Visualization**: Track execution paths and node-by-node audit trails
- ✅ **Incident Management**: Handle and resolve process errors
- ✅ **Test Scenarios**: Run automated BPMN test scenarios
- ✅ **Health Checks**: Monitor API health and readiness
- ✅ **CORS Enabled**: Allow requests from any origin (frontend-friendly)

## Quick Start

```bash
# Build and run the API
cd engine/Novin.Bpmn.Engine.Api
dotnet run

# API will be available at http://localhost:5000
# Swagger UI at http://localhost:5000/swagger
```

## CORS Configuration

The API is configured to allow requests from any origin:

```javascript
// Works from any frontend application
fetch('http://localhost:5000/api/processes/start', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
        processDefinitionId: 'my-process',
        initialVariables: { amount: 100 }
    })
});
```

## Key Endpoints

### Deploy & Start Process
```http
# 1. Deploy BPMN process
POST /api/deployments
{
    "deploymentKey": "order-process",
    "bpmnXml": "<?xml version=\"1.0\"...>",
    "label": "Order Processing"
}

# 2. Start process instance
POST /api/processes/start
{
    "processDefinitionId": "order-process",
    "initialVariables": { "orderId": "123" }
}
```

### List Process Instances
```http
# Get all process instances with filtering and pagination
GET /api/processes?state=Running&skip=0&take=10

# Response: Array of process instances
# [
#   {
#     "id": "guid",
#     "name": "string",
#     "processDefinitionId": "string",
#     "state": "Created|Running|Completed|Failed",
#     "createdAt": "datetime",
#     "startedAt": "datetime?",
#     "completedAt": "datetime?",
#     "variables": { "key": "value" }
#   }
# ]
```

### Monitor Execution
```http
# Get execution flow with visualization data
GET /api/process-execution/{processId}/flow

# Get minimal audit trail
GET /api/process-execution/{processId}/path

# Get processes with BPMN models
GET /api/processes/with-models?includeModel=true
```

### Health Checks
```http
# Basic health check
GET /health

# Readiness probe
GET /health/ready

# Liveness probe
GET /health/live
```

## Test Scenarios

Run automated BPMN test scenarios:

```http
# List available scenarios
GET /api/test-scenarios

# Run specific scenario
POST /api/test-scenarios/ErrorBoundaryScenario/run

# Run all scenarios
POST /api/test-scenarios/run-all
```

## BPMN.js Integration

The API is designed to work seamlessly with BPMN.js:

```javascript
// Load BPMN diagram
const viewer = new BpmnJS({ container: '#canvas' });

// Get execution data
const executionData = await fetch(`/api/process-execution/${processId}/flow`)
    .then(r => r.json());

// Highlight executed elements
executionData.executedElements.forEach(element => {
    const overlay = viewer.get('overlays');
    overlay.add(element.elementId, {
        position: { bottom: 0, right: 0 },
        html: '<div class="execution-indicator">✓</div>'
    });
});
```

## Architecture

- **Domain-Driven Design**: Clean architecture with domain entities
- **CQRS Pattern**: Separate commands and queries
- **Repository Pattern**: Abstracted data access
- **Health Checks**: Built-in monitoring
- **CORS Support**: Frontend-ready configuration

## Available Scenarios

1. **ErrorBoundaryScenario**: Error handling with boundary events
2. **EnterpriseDemoScenario**: Exception handling demonstration
3. **TimerBoundaryScenario**: Timer boundary events testing

## HTTP Test Files

- `BpmnApi.http` - Complete API endpoint reference
- `ExecutionFlow.http` - Execution flow and visualization examples
- `TestScenarios.http` - Test scenario execution examples

All HTTP files contain working examples with CORS-enabled requests.