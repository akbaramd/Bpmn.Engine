using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

/// <summary>
/// Maps BPMN model types to domain NodeType enum
/// </summary>
public static class BpmnNodeTypeMapper
{
    public static NodeType MapToNodeType(BpmnFlowElement element)
    {
        return element switch
        {
            // Events (most specific first)
            BpmnStartEvent => NodeType.StartEvent,
            BpmnEndEvent => NodeType.EndEvent,
            BpmnIntermediateCatchEvent => NodeType.IntermediateEvent,
            BpmnIntermediateThrowEvent => NodeType.IntermediateEvent,
            
            // Tasks (most specific first - derived classes before base class)
            BpmnUserTask => NodeType.UserTask,
            BpmnServiceTask => NodeType.ServiceTask,
            BpmnScriptTask => NodeType.ScriptTask,
            BpmnManualTask => NodeType.ManualTask,
            BpmnTask => NodeType.Task, // Base task class (check after derived classes)
            
            // Gateways (most specific first - derived classes before base class)
            BpmnExclusiveGateway => NodeType.ExclusiveGateway,
            BpmnParallelGateway => NodeType.ParallelGateway,
            BpmnInclusiveGateway => NodeType.InclusiveGateway,
            BpmnEventBasedGateway => NodeType.EventBasedGateway,
            BpmnGateway => NodeType.Gateway, // Base gateway class (check after derived classes)
            
            // SubProcess
            BpmnSubProcess => NodeType.SubProcess,
            
            // Default fallback
            _ => NodeType.Task
        };
    }
}

