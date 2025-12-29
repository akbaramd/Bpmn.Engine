using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Novin.Bpmn.Engine.Clients.Abstractions;
using Novin.Bpmn.Engine.Clients.Services;
using Microsoft.AspNetCore.Mvc;

namespace Novin.Bpmn.Engine.Clients.Extensions;

/// <summary>
/// Extension methods for configuring BPMN engine client endpoints
/// </summary>
public static class WebApplicationExtensions
{
    /// <summary>
    /// Maps BPMN engine client endpoints to the application
    /// </summary>
    /// <param name="app">The web application</param>
    /// <param name="routePrefix">Optional route prefix for the endpoints (default: "bpmn")</param>
    /// <returns>The web application for chaining</returns>
    public static WebApplication MapBpmnEngineEndpoints(
        this WebApplication app,
        string routePrefix = "bpmn")
    {
        if (string.IsNullOrWhiteSpace(routePrefix))
            routePrefix = "bpmn";

        // Ensure trailing slash for proper route building
        if (!routePrefix.EndsWith("/"))
            routePrefix += "/";

        var routeGroup = app.MapGroup(routePrefix.TrimEnd('/'));

        // Client info endpoints
        routeGroup.MapGet("client/info", GetClientInfo)
            .WithName("GetClientInfo")
            .WithDescription("Get information about this BPMN engine client");

        routeGroup.MapGet("client/health", GetClientHealth)
            .WithName("GetClientHealth")
            .WithDescription("Get health status of this BPMN engine client");

        // Service worker endpoints
        routeGroup.MapGet("workers", GetRegisteredWorkers)
            .WithName("GetRegisteredWorkers")
            .WithDescription("Get all registered service workers");

        routeGroup.MapGet("workers/{workerId}", GetWorkerById)
            .WithName("GetWorkerById")
            .WithDescription("Get a specific service worker by ID");

        routeGroup.MapGet("workers/{workerId}/status", GetWorkerStatus)
            .WithName("GetWorkerStatus")
            .WithDescription("Get the current status of a service worker");

        // Connection management endpoints
        routeGroup.MapPost("connect", ConnectToEngine)
            .WithName("ConnectToEngine")
            .WithDescription("Connect this client to the BPMN engine");

        routeGroup.MapPost("disconnect", DisconnectFromEngine)
            .WithName("DisconnectFromEngine")
            .WithDescription("Disconnect this client from the BPMN engine");

        routeGroup.MapGet("connection/status", GetConnectionStatus)
            .WithName("GetConnectionStatus")
            .WithDescription("Get the current connection status to the BPMN engine");

        return app;
    }

    private static async Task<IResult> GetClientInfo(
        BpmnClientOptions options,
        IServiceWorkerRegistry workerRegistry)
    {
        var clientInfo = new
        {
            ClientId = options.ClientId,
            EngineBaseUrl = options.EngineBaseUrl,
            MaxConcurrentWorkItems = options.MaxConcurrentWorkItems,
            RegisteredWorkersCount = workerRegistry.GetAllWorkers().Count(),
            SupportedWorkTypes = workerRegistry.GetAllWorkers()
                .SelectMany(w => w.SupportedWorkTypes)
                .Distinct()
                .ToList()
        };

        return Results.Ok(clientInfo);
    }

    private static async Task<IResult> GetClientHealth(
        BpmnClientOptions options,
        IServiceWorkerRegistry workerRegistry)
    {
        var healthStatus = new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            ClientId = options.ClientId,
            ActiveWorkers = workerRegistry.GetAllWorkers().Count(w => w.Enabled),
            TotalWorkers = workerRegistry.GetAllWorkers().Count(),
            WebhookUrl = options.EngineBaseUrl
        };

        return Results.Ok(healthStatus);
    }

    private static async Task<IResult> GetRegisteredWorkers(
        IServiceWorkerRegistry workerRegistry)
    {
        var workers = workerRegistry.GetAllWorkers().Select(w => new
        {
            w.WorkerId,
            w.Name,
            w.Description,
            w.SupportedWorkTypes,
            w.MaxConcurrentTasks,
            w.Enabled,
            HandlerType = w.HandlerType?.Name
        });

        return Results.Ok(workers);
    }

    private static async Task<IResult> GetWorkerById(
        string workerId,
        IServiceWorkerRegistry workerRegistry)
    {
        var worker = workerRegistry.GetWorker(workerId);
        if (worker == null)
            return Results.NotFound(new { error = $"Worker '{workerId}' not found" });

        var workerInfo = new
        {
            worker.WorkerId,
            worker.Name,
            worker.Description,
            worker.SupportedWorkTypes,
            worker.MaxConcurrentTasks,
            worker.Enabled,
            HandlerType = worker.HandlerType?.Name
        };

        return Results.Ok(workerInfo);
    }

    private static async Task<IResult> ConnectToEngine(IClientConnectionManager connectionManager)
    {
        try
        {
            await connectionManager.RegisterWithEngineAsync();
            return Results.Ok(new { message = "Connected to BPMN engine successfully" });
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message, statusCode: 500);
        }
    }

    private static async Task<IResult> DisconnectFromEngine(IClientConnectionManager connectionManager)
    {
        try
        {
            await connectionManager.UnregisterFromEngineAsync();
            return Results.Ok(new { message = "Disconnected from BPMN engine successfully" });
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message, statusCode: 500);
        }
    }

    private static async Task<IResult> GetConnectionStatus(IClientConnectionManager connectionManager)
    {
        var status = connectionManager.GetConnectionStatus();
        return Results.Ok(new
        {
            status.IsConnected,
            status.ConnectionId,
            status.LastConnectionAttempt,
            status.LastConnectedAt,
            status.LastDisconnectedAt,
            status.ErrorMessage
        });
    }

    private static async Task<IResult> GetWorkerStatus(
        string workerId,
        IServiceWorkerRegistry workerRegistry)
    {
        var worker = workerRegistry.GetWorker(workerId);
        if (worker == null)
            return Results.NotFound(new { error = $"Worker '{workerId}' not found" });

        // In a real implementation, this would return actual runtime status
        var status = new
        {
            WorkerId = worker.WorkerId,
            Enabled = worker.Enabled,
            ActiveTasks = 0, // This would be tracked in a real implementation
            PendingTasks = 0, // This would be tracked in a real implementation
            LastActivity = DateTime.UtcNow // This would be tracked in a real implementation
        };

        return Results.Ok(status);
    }

}