using Microsoft.AspNetCore.Mvc;
using Novin.Bpmn.Engine.Api.TestScenarios;

namespace Novin.Bpmn.Engine.Api.Controllers;

/// <summary>
/// Controller for managing BPMN test scenarios execution
/// </summary>
[ApiController]
[Route("api/test-scenarios")]
public sealed class TestScenariosController : ControllerBase
{
    private readonly TestScenarioRunner _scenarioRunner;
    private readonly ILogger<TestScenariosController> _logger;

    public TestScenariosController(
        TestScenarioRunner scenarioRunner,
        ILogger<TestScenariosController> logger)
    {
        _scenarioRunner = scenarioRunner ?? throw new ArgumentNullException(nameof(scenarioRunner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get all available test scenarios
    /// </summary>
    /// <returns>List of available test scenarios</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TestScenarioInfo>), 200)]
    public async Task<ActionResult<IEnumerable<TestScenarioInfo>>> GetScenarios()
    {
        var scenarios = new List<TestScenarioInfo>();

        foreach (var scenario in TestScenarioRunner.GetAllScenarios())
        {
            var testCases = await scenario.GetTestCasesAsync();
            scenarios.Add(new TestScenarioInfo
            {
                Name = scenario.Name,
                Description = scenario.Description,
                TestCases = testCases.Select(tc => new TestCaseInfo
                {
                    Name = tc.Name,
                    Description = tc.Description,
                    Variables = tc.Variables ?? new Dictionary<string, string>()
                }).ToList()
            });
        }

        return Ok(scenarios);
    }

    /// <summary>
    /// Run a specific test scenario by name
    /// </summary>
    /// <param name="scenarioName">Name of the scenario to run</param>
    /// <returns>Execution result</returns>
    [HttpPost("{scenarioName}/run")]
    [ProducesResponseType(typeof(ScenarioExecutionResult), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ScenarioExecutionResult>> RunScenario(
        string scenarioName,
        CancellationToken ct = default)
    {
        var scenario = TestScenarioRunner.FindScenario(scenarioName);
        if (scenario == null)
        {
            return NotFound(new { error = $"Scenario '{scenarioName}' not found" });
        }

        try
        {
            _logger.LogInformation("Starting scenario execution: {ScenarioName}", scenarioName);

            await _scenarioRunner.RunScenarioAsync(scenario, ct);

            _logger.LogInformation("Scenario execution completed: {ScenarioName}", scenarioName);

            return Ok(new ScenarioExecutionResult
            {
                ScenarioName = scenario.Name,
                Status = "Completed",
                ExecutedAt = DateTime.UtcNow,
                Message = $"Scenario '{scenario.Name}' executed successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing scenario: {ScenarioName}", scenarioName);

            return StatusCode(500, new ScenarioExecutionResult
            {
                ScenarioName = scenario.Name,
                Status = "Failed",
                ExecutedAt = DateTime.UtcNow,
                Message = $"Scenario execution failed: {ex.Message}",
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Run all available test scenarios
    /// </summary>
    /// <returns>Execution results for all scenarios</returns>
    [HttpPost("run-all")]
    [ProducesResponseType(typeof(IEnumerable<ScenarioExecutionResult>), 200)]
    public async Task<ActionResult<IEnumerable<ScenarioExecutionResult>>> RunAllScenarios(
        CancellationToken ct = default)
    {
        var allScenarios = TestScenarioRunner.GetAllScenarios();
        var results = new List<ScenarioExecutionResult>();

        _logger.LogInformation("Starting execution of all scenarios ({Count} scenarios)", allScenarios.Count);

        foreach (var scenario in allScenarios)
        {
            try
            {
                _logger.LogInformation("Executing scenario: {ScenarioName}", scenario.Name);

                await _scenarioRunner.RunScenarioAsync(scenario, ct);

                results.Add(new ScenarioExecutionResult
                {
                    ScenarioName = scenario.Name,
                    Status = "Completed",
                    ExecutedAt = DateTime.UtcNow,
                    Message = $"Scenario '{scenario.Name}' executed successfully"
                });

                _logger.LogInformation("Scenario completed: {ScenarioName}", scenario.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing scenario: {ScenarioName}", scenario.Name);

                results.Add(new ScenarioExecutionResult
                {
                    ScenarioName = scenario.Name,
                    Status = "Failed",
                    ExecutedAt = DateTime.UtcNow,
                    Message = $"Scenario execution failed: {ex.Message}",
                    Error = ex.Message
                });
            }
        }

        _logger.LogInformation("All scenarios execution completed");

        return Ok(results);
    }

    /// <summary>
    /// Get scenario execution status (for future implementation)
    /// </summary>
    /// <param name="scenarioName">Name of the scenario</param>
    /// <returns>Execution status</returns>
    [HttpGet("{scenarioName}/status")]
    [ProducesResponseType(typeof(ScenarioStatus), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ScenarioStatus>> GetScenarioStatus(string scenarioName)
    {
        var scenario = TestScenarioRunner.FindScenario(scenarioName);
        if (scenario == null)
        {
            return NotFound(new { error = $"Scenario '{scenarioName}' not found" });
        }

        // For now, just return basic info since we don't track execution status yet
        var testCases = await scenario.GetTestCasesAsync();
        return Ok(new ScenarioStatus
        {
            ScenarioName = scenario.Name,
            Description = scenario.Description,
            TestCaseCount = testCases.Count,
            IsRunning = false, // Future: track actual execution status
            LastExecutedAt = null // Future: track last execution time
        });
    }
}

/// <summary>
/// Information about a test scenario
/// </summary>
public sealed class TestScenarioInfo
{
    public string Name { get; init; } = default!;
    public string Description { get; init; } = default!;
    public IReadOnlyList<TestCaseInfo> TestCases { get; init; } = Array.Empty<TestCaseInfo>();
}

/// <summary>
/// Information about a test case
/// </summary>
public sealed class TestCaseInfo
{
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public IReadOnlyDictionary<string, string> Variables { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// Result of scenario execution
/// </summary>
public sealed class ScenarioExecutionResult
{
    public string ScenarioName { get; init; } = default!;
    public string Status { get; init; } = default!; // "Completed" or "Failed"
    public DateTime ExecutedAt { get; init; }
    public string Message { get; init; } = default!;
    public string? Error { get; init; }
}

/// <summary>
/// Status of a scenario
/// </summary>
public sealed class ScenarioStatus
{
    public string ScenarioName { get; init; } = default!;
    public string Description { get; init; } = default!;
    public int TestCaseCount { get; init; }
    public bool IsRunning { get; init; }
    public DateTime? LastExecutedAt { get; init; }
}