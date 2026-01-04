using MediatR;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Api.TestScenarios;

/// <summary>
/// Runs test scenarios
/// </summary>
public sealed class TestScenarioRunner
{
    private readonly IMediator _mediator;
    private readonly ILogger<TestScenarioRunner> _logger;
    private readonly IUnitOfWork _uow;

    public TestScenarioRunner(
        IMediator mediator,
        ILogger<TestScenarioRunner> logger,
        IUnitOfWork uow)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
    }

    public async Task RunScenarioAsync(TestScenario scenario, CancellationToken ct = default)
    {
        _logger.LogInformation("=== {ScenarioName} ===", scenario.Name);
        _logger.LogInformation("Description: {Description}", scenario.Description);
        _logger.LogInformation("");

        try
        {
            await EnsureDeploymentAsync(scenario, ct);

            // Get test cases
            var testCases = await scenario.GetTestCasesAsync();

            // Run test cases
            _logger.LogInformation("1. Running test cases...");
            for (int i = 0; i < testCases.Count; i++)
            {
                var testCase = testCases[i];
                _logger.LogInformation(
                    "   Test {Index}: {TestCaseName}",
                    i + 1,
                    testCase.Name);
                _logger.LogInformation("   Description: {Description}", testCase.Description);

                var processId = await scenario.CreateAndStartProcessAsync(
                    _mediator,
                    _logger,
                    testCase.Name,
                    testCase.Variables,
                    ct);

                await Task.Delay(testCase.WaitMilliseconds, ct);

                // Check process completion status
                var process = await _uow.Processes.GetByIdAsync(processId, ct);
                if (process == null)
                {
                    _logger.LogError("   ❌ Process not found. ProcessId: {ProcessId}", processId);
                }
                else
                {
                    var isCompleted = process.State == ProcessState.Completed;
                    var isFailed = process.State == ProcessState.Failed;

                    // Display execution path if available
                    if (process.TryGetVariable<string>("executionPath", out var executionPath))
                    {
                        _logger.LogInformation("   📍 Execution Path: {ExecutionPath}", executionPath);
                    }

                    if (isCompleted)
                    {
                        _logger.LogInformation("   ✅ Process completed successfully");
                    }
                    else if (isFailed)
                    {
                        _logger.LogError("   ❌ Process failed");
                    }
                    else
                    {
                        _logger.LogWarning("   ⚠️ Process still running or in unexpected state: {State}", process.State);
                    }
                }

                _logger.LogInformation("");
            }

            _logger.LogInformation("=== {ScenarioName} Completed ===", scenario.Name);
            _logger.LogInformation("");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running scenario: {ScenarioName}", scenario.Name);
            throw;
        }
    }

    private async Task EnsureDeploymentAsync(TestScenario scenario, CancellationToken ct)
    {
        if (scenario.GetDeploymentId() != Guid.Empty)
        {
            _logger.LogInformation("Using existing deployment {DeploymentId} for scenario {Scenario}",
                scenario.GetDeploymentId(), scenario.Name);
            return;
        }

        // Try to find existing active deployment
        var existingDeployment = await _uow.Deployments.GetLatestByDeploymentKeyAsync(scenario.ProcessKey, ct);
        if (existingDeployment != null && existingDeployment.IsActive)
        {
            scenario.SetDeploymentId(existingDeployment.Id);
            _logger.LogInformation("Using existing deployment {DeploymentId} (key={DeploymentKey}, version={Version}) for scenario {Scenario}",
                existingDeployment.Id, existingDeployment.DeploymentKey, existingDeployment.Version, scenario.Name);
            return;
        }

        // Deploy from BPMN file
        _logger.LogInformation("Deploying BPMN file '{BpmnFileName}' for scenario {Scenario}",
            scenario.BpmnFileName, scenario.Name);

        // Try multiple possible paths for BPMN file
        var possiblePaths = new[]
        {
            Path.Combine("Bpmn", scenario.BpmnFileName),
            Path.Combine(AppContext.BaseDirectory, "Bpmn", scenario.BpmnFileName),
            Path.Combine(Directory.GetCurrentDirectory(), "Bpmn", scenario.BpmnFileName),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Bpmn", scenario.BpmnFileName)
        };

        string? bpmnFilePath = null;
        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                bpmnFilePath = path;
                break;
            }
        }

        if (bpmnFilePath == null)
        {
            var searchedPaths = string.Join(", ", possiblePaths);
            throw new FileNotFoundException(
                $"BPMN file not found: {scenario.BpmnFileName}. Searched paths: {searchedPaths}. Please ensure the file exists in the Bpmn directory.");
        }

        _logger.LogDebug("Found BPMN file at: {BpmnFilePath}", bpmnFilePath);

        var bpmnXml = await File.ReadAllTextAsync(bpmnFilePath, ct);
        if (string.IsNullOrWhiteSpace(bpmnXml))
        {
            throw new InvalidOperationException($"BPMN file is empty: {bpmnFilePath}");
        }

        // Get next version for this deployment key
        var nextVersion = await _uow.Deployments.GetNextVersionAsync(scenario.ProcessKey, ct);

        // Create deployment
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var deployment =  Deployment.Create(
                Guid.Empty, 
                deploymentKey: scenario.ProcessKey,
                bpmnXml: bpmnXml,
                label: $"{scenario.Name} - {scenario.BpmnFileName}");

            await _uow.Deployments.AddAsync(deployment, ct);
            await _uow.CommitTransactionAsync(ct);

            scenario.SetDeploymentId(deployment.Id);
            _logger.LogInformation("✓ Deployed BPMN file '{BpmnFileName}'. DeploymentId={DeploymentId} Key={DeploymentKey} Version={Version}",
                scenario.BpmnFileName, deployment.Id, deployment.DeploymentKey, deployment.Version);
        }
        catch (Exception ex)
        {
            await _uow.RollbackTransactionAsync(ct);
            _logger.LogError(ex, "Failed to deploy BPMN file '{BpmnFileName}'", scenario.BpmnFileName);
            throw;
        }
    }

    public static IReadOnlyList<TestScenario> GetAllScenarios()
    {
        return new List<TestScenario>
        {
            new EnterpriseDemoScenario(),
            new ErrorBoundaryScenario(),
            new TimerBoundaryScenario(),
            new MathSumScenario()
        };
    }

    public static TestScenario? FindScenario(string name)
    {
        return GetAllScenarios()
            .FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
