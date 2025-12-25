using MediatR;
using Novin.Bpmn.Engine.Application.Commands.DeployProcess;
using Novin.Bpmn.Engine.Application.Commands.StartProcess;

namespace Novin.Bpmn.Engine.Api.TestScenarios;

/// <summary>
/// Base class for test scenarios
/// </summary>
public abstract class TestScenario
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract string BpmnFileName { get; }
    public abstract string ProcessKey { get; }

    public abstract Task<IReadOnlyList<TestCase>> GetTestCasesAsync();

    public async Task<Guid> DeployProcessAsync(
        IMediator mediator,
        ILogger logger,
        CancellationToken ct = default)
    {
        var bpmnFilePath = Path.Combine(AppContext.BaseDirectory, "Bpmn", BpmnFileName);
        if (!File.Exists(bpmnFilePath))
        {
            throw new FileNotFoundException($"BPMN file not found: {bpmnFilePath}");
        }

        var bpmnXml = await File.ReadAllTextAsync(bpmnFilePath, ct);

        var deployCommand = new DeployProcessCommand(
            ProcessKey,
            bpmnXml,
            $"{Name} - Test Scenario Deployment");

        var deployResult = await mediator.Send(deployCommand, ct);

        logger.LogInformation(
            "   ✓ Process deployed. DeploymentId: {DeploymentId}, Version: {Version}",
            deployResult.DeploymentId,
            deployResult.Version);

        return deployResult.DeploymentId;
    }

    public async Task<Guid> StartProcessAsync(
        IMediator mediator,
        ILogger logger,
        string testCaseName,
        Dictionary<string, object>? variables = null,
        CancellationToken ct = default)
    {
        var startCommand = new StartProcessCommand(
            ProcessKey,
            $"{Name} - {testCaseName}",
            variables ?? new Dictionary<string, object>());

        var startResult = await mediator.Send(startCommand, ct);

        logger.LogInformation(
            "   ✓ Process started. ProcessId: {ProcessId}",
            startResult.ProcessId);

        return startResult.ProcessId;
    }
}

/// <summary>
/// Represents a test case within a scenario
/// </summary>
public record TestCase(
    string Name,
    string Description,
    Dictionary<string, object>? Variables = null,
    int WaitMilliseconds = 2000);
