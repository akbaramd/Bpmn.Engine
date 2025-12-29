using Novin.Bpmn.Engine.Application.Common.Interfaces;

namespace Novin.Bpmn.Engine.Application.Services;

/// <summary>
/// Default implementation of IClock using system clock
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.UtcNow;
    public DateTime UtcNow => DateTime.UtcNow;
}
