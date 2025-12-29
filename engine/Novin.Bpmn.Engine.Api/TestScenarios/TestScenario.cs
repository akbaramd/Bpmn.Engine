using MediatR;
using Novin.Bpmn.Engine.Application.Commands.CreateProcessInstance;
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
    public abstract string ProcessBpmnId { get; }

    public Guid DeploymentId { get; private set; }

    public abstract Task<IReadOnlyList<TestCase>> GetTestCasesAsync();

    public void SetDeploymentId(Guid deploymentId)
    {
        DeploymentId = deploymentId;
    }

    public Guid GetDeploymentId() => DeploymentId;

    /// <summary>
    /// Creates a process instance (without deployment) and then starts it.
    /// Assumes deployment is already present and referenced by DeploymentId.
    /// </summary>
    public async Task<Guid> CreateAndStartProcessAsync(
        IMediator mediator,
        ILogger logger,
        string testCaseName,
        Dictionary<string, string>? variables = null,
        CancellationToken ct = default)
    {
        if (DeploymentId == Guid.Empty)
        {
            throw new InvalidOperationException("DeploymentId not set. Please set it before running scenarios.");
        }

        var objectVars = variables?.ToDictionary(kv => kv.Key, kv => (object?)kv.Value)
            ?? new Dictionary<string, object?>();

        var createCommand = new CreateProcessInstanceCommand(
            DeploymentId,
            ProcessBpmnId,
            $"{Name} - {testCaseName}",
            objectVars);

        var createResult = await mediator.Send(createCommand, ct);
        logger.LogInformation("   ✓ Process instance created. ProcessId: {ProcessId}", createResult.ProcessId);

        var startCommand = new StartProcessCommand(createResult.ProcessId);
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
    Dictionary<string, string>? Variables = null,
    int WaitMilliseconds = 2000);
