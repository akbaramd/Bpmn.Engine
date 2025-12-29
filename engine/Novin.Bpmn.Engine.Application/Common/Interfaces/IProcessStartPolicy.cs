using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.Common.Interfaces;

/// <summary>
/// Strategy for deciding whether a created process should be started automatically.
/// </summary>
public interface IProcessStartPolicy
{
    bool ShouldAutoStart(Process process);
}

