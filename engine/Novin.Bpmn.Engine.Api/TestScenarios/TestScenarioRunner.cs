using MediatR;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
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
            // Deploy process
            _logger.LogInformation("1. Deploying process...");
            await scenario.DeployProcessAsync(_mediator, _logger, ct);

            // Get test cases
            var testCases = await scenario.GetTestCasesAsync();

            // Run test cases
            _logger.LogInformation("2. Running test cases...");
            for (int i = 0; i < testCases.Count; i++)
            {
                var testCase = testCases[i];
                _logger.LogInformation(
                    "   Test {Index}: {TestCaseName}",
                    i + 1,
                    testCase.Name);
                _logger.LogInformation("   Description: {Description}", testCase.Description);

                var processId = await scenario.StartProcessAsync(
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
                    if (process.Variables.TryGetValue("executionPath", out var executionPath))
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

    public static IReadOnlyList<TestScenario> GetAllScenarios()
    {
        return new List<TestScenario>
        {
            new EnterpriseDemoScenario(),
            new ErrorBoundaryScenario(),
            new TimerBoundaryScenario()
        };
    }

    public static TestScenario? FindScenario(string name)
    {
        return GetAllScenarios()
            .FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
