using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Common.Interfaces;

/// <summary>
/// Compiled/executable version of a BPMN process definition.
/// This is what gets stored in memory - pre-parsed and ready to use.
/// </summary>
public sealed class ExecutableProcessDefinition
{
    public ProcessDefinitionRef Ref { get; }
    public BpmnProcess Process { get; }
    public BpmnDefinitions Definitions { get; }
    public DateTime CompiledAtUtc { get; }

    public ExecutableProcessDefinition(
        ProcessDefinitionRef @ref,
        BpmnProcess process,
        BpmnDefinitions definitions,
        DateTime compiledAtUtc)
    {
        Ref = @ref ?? throw new ArgumentNullException(nameof(@ref));
        Process = process ?? throw new ArgumentNullException(nameof(process));
        Definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        CompiledAtUtc = compiledAtUtc;
    }
}

