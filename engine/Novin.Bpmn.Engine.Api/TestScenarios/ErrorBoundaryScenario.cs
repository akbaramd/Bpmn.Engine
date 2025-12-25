using MediatR;

namespace Novin.Bpmn.Engine.Api.TestScenarios;

/// <summary>
/// Error boundary scenario - tests error boundary event handling
/// </summary>
public sealed class ErrorBoundaryScenario : TestScenario
{
    public override string Name => "Error Boundary Test";
    public override string Description => "Tests error boundary event (interrupting)";
    public override string BpmnFileName => "error-boundary-test.bpmn";
    public override string ProcessKey => "error-boundary-test";

    public override async Task<IReadOnlyList<TestCase>> GetTestCasesAsync()
    {
        return new List<TestCase>
        {
            new(
                "Error Thrown Test",
                "Tests error boundary when error is thrown (should be caught by boundary)",
                new Dictionary<string, object>
                {
                    { "shouldThrowError", true },
                    { "errorCode", "TEST_ERROR" },
                    { "executionPath", "start" } // Initialize execution path
                },
                2000),
            new(
                "No Error Test",
                "Tests normal flow when no error is thrown",
                new Dictionary<string, object>
                {
                    { "shouldThrowError", false },
                    { "executionPath", "start" } // Initialize execution path
                },
                2000)
        };
    }
}
