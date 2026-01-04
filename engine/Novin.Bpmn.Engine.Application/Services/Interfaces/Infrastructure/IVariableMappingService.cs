using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

public interface IVariableMappingService
{
    void ApplyInputs(Process process, Token token, NodeInstance node, BpmnFlowElement element, BpmnRuntimeContext ctx);
    void ApplyOutputs(Process process, Token token, NodeInstance node, BpmnFlowElement element, BpmnRuntimeContext ctx);
}