using MediatR;

namespace Novin.Bpmn.Engine.Api.TestScenarios;

/// <summary>
/// Enterprise demo scenario - tests exception handling
/// </summary>
public sealed class EnterpriseDemoScenario : TestScenario
{
    public override string Name => "EnterpriseDemoScenario";
    public override string Description => "Tests exception handling (Technical Failure and BPMN Error)";
    public override string BpmnFileName => "enterprise-demo.bpmn";
    public override string ProcessKey => "demo-process-key";
    public override string ProcessBpmnId => "Process_1";

    public override Task<IReadOnlyList<TestCase>> GetTestCasesAsync()
    {
        return Task.FromResult<IReadOnlyList<TestCase>>(new List<TestCase>
        {
            new(
                "Technical Failure Test",
                "Tests technical failure handling in fraudCheck (amount > 10000)",
                new Dictionary<string, string> { { "amount", "15000" } },
                2000),
            new(
                "BPMN Error Test",
                "Tests BPMN error handling in vipDiscount (amount < 0)",
                new Dictionary<string, string> { { "amount", "-100" } },
                2000),
            new(
                "Normal Flow Test",
                "Tests normal flow execution (amount = 500)",
                new Dictionary<string, string> { { "amount", "500" } },
                3000)
        });
    }
}
