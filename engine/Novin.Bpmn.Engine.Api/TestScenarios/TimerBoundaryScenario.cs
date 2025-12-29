using MediatR;

namespace Novin.Bpmn.Engine.Api.TestScenarios;

/// <summary>
/// Timer boundary scenario - tests interrupting and non-interrupting timer boundaries
/// </summary>
public sealed class TimerBoundaryScenario : TestScenario
{
    public override string Name => "TimerBoundaryScenario";
    public override string Description => "Tests timer boundary events (interrupting and non-interrupting)";
    public override string BpmnFileName => "timer-boundary-test.bpmn";
    public override string ProcessKey => "timer-boundary-test";
    public override string ProcessBpmnId => "Process_1";

    public override Task<IReadOnlyList<TestCase>> GetTestCasesAsync()
    {
        return Task.FromResult<IReadOnlyList<TestCase>>(new List<TestCase>
        {
            new(
                "Interrupting Timer Test",
                "Tests interrupting timer boundary (2s timer, task duration 5s - should interrupt)",
                new Dictionary<string, string>
                {
                    { "duration", "5000" } // 5 seconds - timer will interrupt after 2s
                },
                3000), // Wait for timer to fire
            new(
                "Non-Interrupting Timer Test",
                "Tests non-interrupting timer boundary (1s timer, task duration 3s - should run in parallel)",
                new Dictionary<string, string>
                {
                    { "duration", "3000" } // 3 seconds - timer fires after 1s but task continues
                },
                4000), // Wait for both to complete
            new(
                "Task Completes Before Timer Test",
                "Tests that timer is canceled when task completes before timer fires",
                new Dictionary<string, string>
                {
                    { "duration", "500" } // 0.5 seconds - completes before 1s timer fires
                },
                2000)
        });
    }
}
