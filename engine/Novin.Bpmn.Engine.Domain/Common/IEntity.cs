namespace Novin.Bpmn.Engine.Domain.Common;

/// <summary>
/// Base interface for all domain entities
/// </summary>
public interface IEntity
{
    Guid Id { get; }
}

