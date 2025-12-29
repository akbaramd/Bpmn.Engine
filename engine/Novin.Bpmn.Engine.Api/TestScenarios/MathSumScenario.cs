using MediatR;

namespace Novin.Bpmn.Engine.Api.TestScenarios;

/// <summary>
/// Math sum scenario - tests service task with external client calculation
/// </summary>
public sealed class MathSumScenario : TestScenario
{
    public override string Name => "MathSumScenario";
    public override string Description => "Tests service task that performs math calculations using external client";
    public override string BpmnFileName => "math-sum-test.bpmn";
    public override string ProcessKey => "math-sum-test";
    public override string ProcessBpmnId => "math-sum-test";

    public override Task<IReadOnlyList<TestCase>> GetTestCasesAsync()
    {
        return Task.FromResult<IReadOnlyList<TestCase>>(new List<TestCase>
        {
            new(
                "Basic Sum Test",
                "Tests basic addition: 15.5 + 24.7 = 40.2",
                new Dictionary<string, string>
                {
                    // No input variables needed - numbers are set in the first script task
                },
                3000) // Allow more time for external service call
         
        });
    }
}