namespace Novin.Bpmn.Engine.Application.Services;

public sealed class MultiLanguageScriptTaskExecutorOptions
{
    // If scriptFormat is null/empty => treat as C#
    public bool TreatNullFormatAsCSharp { get; init; } = true;

    public TimeSpan CSharpTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan JavaScriptTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public int JavaScriptMaxStatements { get; init; } = 10_000;
    public long JavaScriptMaxMemoryBytes { get; init; } = 4_000_000; // 4MB
}