using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;

namespace Novin.Bpmn.Engine.Application.BackgroundServices;

/// <summary>
/// Hosted service that warms up all BPMN definitions on startup.
/// </summary>
public sealed class DefinitionWarmupHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DefinitionWarmupHostedService> _logger;

    public DefinitionWarmupHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<DefinitionWarmupHostedService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting BPMN definition warm-up...");

        using var scope = _scopeFactory.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IExecutableDefinitionCatalog>();

        try
        {
            await catalog.WarmUpAllAsync(ct);
            _logger.LogInformation("BPMN definition warm-up completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to warm up BPMN definitions");
            // Don't throw - allow application to start even if warm-up fails
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

