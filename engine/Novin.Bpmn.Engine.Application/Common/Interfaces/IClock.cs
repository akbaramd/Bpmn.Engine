namespace Novin.Bpmn.Engine.Application.Common.Interfaces;

/// <summary>
/// Abstraction برای clock - قابل mock برای testing
/// </summary>
public interface IClock
{
    DateTimeOffset Now { get; }
    DateTime UtcNow { get; }
}
