namespace Novin.Bpmn.Engine.Application.Services;

public sealed class ProcessRuntimeOptions
{
    /// <summary>
    /// If true, processes are started automatically when created (default).
    /// </summary>
    public bool AutoStartOnCreate { get; set; } = false;
}

