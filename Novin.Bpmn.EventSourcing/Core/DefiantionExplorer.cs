// create class

using Novin.Bpmn.EventSourcing.Events;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.EventSourcing.Core;

public class DefiantionExplorer
{
    public DefiantionExplorer(BpmnDefinitions definitions)
    {
        Definitions = definitions;
    }

    public BpmnDefinitions Definitions { get; }

    public virtual List<BpmnProcess> FindProcesses()
    {
        var processes = new List<BpmnProcess>();

        if (Definitions?.Items == null)
            return processes;

        return Definitions.Items.OfType<BpmnProcess>().ToList();
    }
    public virtual BpmnProcess? FindProcess(string processId)
    {
        var processes = FindProcesses();
        return processes.FirstOrDefault(p => p.id == processId);
    }
    public virtual List<BpmnStartEvent> FindStartEvents()
    {
        var startEvents = new List<BpmnStartEvent>();

        if (Definitions?.Items == null)
            return startEvents;

        // First find all processes
        var processes = FindProcesses();

        foreach (var process in processes)
        {
            if (process.Items == null)
                continue;

            startEvents.AddRange(process.Items.OfType<BpmnStartEvent>());
        }

        return startEvents;
    }


    public virtual BpmnStartEvent? FindStartEvents(string processId)
    {
        var process = FindProcess(processId);

        if (process == null)
            return null;

        return process.Items.OfType<BpmnStartEvent>().FirstOrDefault();
    }

    public virtual BpmnScriptTask? FindScriptTask(string processId, string elementId)
    {

        // find with process id
        var process = FindProcess(processId);

        if (process == null)
            return null;

        return process.Items.OfType<BpmnScriptTask>().FirstOrDefault(t => t.id == elementId);
    }

    public virtual BpmnUserTask? FindUserTask(string processId, string elementId)
    {
        var process = FindProcess(processId);

        if (process == null)
            return null;

        return process.Items.OfType<BpmnUserTask>().FirstOrDefault(t => t.id == elementId);
    }

    public virtual BpmnServiceTask? FindServiceTask(string processId, string elementId)
    {
        var process = FindProcess(processId);

        if (process == null)
            return null;

        return process.Items.OfType<BpmnServiceTask>().FirstOrDefault(t => t.id == elementId);
    }

    public virtual BpmnBusinessRuleTask? FindBusinessRuleTask(string processId, string elementId)
    {
        var process = FindProcess(processId);

        if (process == null)
            return null;

        return process.Items.OfType<BpmnBusinessRuleTask>().FirstOrDefault(t => t.id == elementId);
    }

    public virtual BpmnReceiveTask? FindReceiveTask(string processId, string elementId)
    {
        var process = FindProcess(processId);

        if (process == null)
            return null;

        return process.Items.OfType<BpmnReceiveTask>().FirstOrDefault(t => t.id == elementId);
    }

    public virtual BpmnSendTask? FindSendTask(string processId, string elementId)
    {
        var process = FindProcess(processId);

        if (process == null)
            return null;

        return process.Items.OfType<BpmnSendTask>().FirstOrDefault(t => t.id == elementId); 
    }

    public virtual BpmnManualTask? FindManualTask(string processId, string elementId)
    {
        var process = FindProcess(processId);

        if (process == null)
            return null;

        return process.Items.OfType<BpmnManualTask>().FirstOrDefault(t => t.id == elementId);
    }   

    public virtual BpmnExclusiveGateway? FindExclusiveGateway(string processId, string elementId)
    {
        var process = FindProcess(processId);

        if (process == null)
            return null;

        return process.Items.OfType<BpmnExclusiveGateway>().FirstOrDefault(t => t.id == elementId);
    }

    public virtual BpmnInclusiveGateway? FindInclusiveGateway(string processId, string elementId)
    {
        var process = FindProcess(processId);

        if (process == null)
            return null;

        return process.Items.OfType<BpmnInclusiveGateway>().FirstOrDefault(t => t.id == elementId);
    }

    public virtual BpmnParallelGateway? FindParallelGateway(string processId, string elementId)
    {
        var process = FindProcess(processId);

        if (process == null)
            return null;

        return process.Items.OfType<BpmnParallelGateway>().FirstOrDefault(t => t.id == elementId);  
    }   

    public virtual BpmnEventBasedGateway? FindEventBasedGateway(string processId, string elementId)
    {
        var process = FindProcess(processId);

        if (process == null)
            return null;

        return process.Items.OfType<BpmnEventBasedGateway>().FirstOrDefault(t => t.id == elementId);
    
    }

    public virtual BpmnSequenceFlow? FindSequenceFlow(string processId, string elementId)
    {
        var process = FindProcess(processId);

        if (process == null)
            return null;

        return process.Items.OfType<BpmnSequenceFlow>().FirstOrDefault(t => t.id == elementId);
    }


    //findincomming with processid and elementid
    public virtual List<BpmnSequenceFlow> FindIncommingSequenceFlows(string processId, string elementId)
    {
        var process = FindProcess(processId);

        if (process == null)
            return null;

        return process.Items.OfType<BpmnSequenceFlow>().Where(t => t.targetRef == elementId).ToList();
    }

    //findoutgoing with processid and elementid
    public virtual List<BpmnSequenceFlow> FindOutgoingSequenceFlows(string processId, string elementId)
    {
        var process = FindProcess(processId);

        if (process == null)
            return null;

        return process.Items.OfType<BpmnSequenceFlow>().Where(t => t.sourceRef == elementId).ToList();
    }

    public virtual BpmnFlowNode? FindTargetElement(string processId, string elementId)
    {
        var process = FindProcess(processId);

        if (process == null)
            return null;

        return process.Items.OfType<BpmnFlowNode>().FirstOrDefault(t => t.id == elementId);
    }

    // convert bpmnnodeelelemtn to elemetntype 
    public virtual BpmnElementType ConvertBpmnNodeToElementType(BpmnFlowNode node)
    {
        // use switchcase  with 
        switch (node)
        {
            case BpmnStartEvent:
                return BpmnElementType.StartEvent;
            case BpmnEndEvent:
                return BpmnElementType.EndEvent;
            case BpmnScriptTask:
                return BpmnElementType.ScriptTask;
            case BpmnUserTask:
                return BpmnElementType.UserTask;
            case BpmnServiceTask:
                return BpmnElementType.ServiceTask; 
            case BpmnBusinessRuleTask:
                return BpmnElementType.BusinessRuleTask;
            case BpmnReceiveTask:
                return BpmnElementType.ReceiveTask;
            case BpmnSendTask:
                return BpmnElementType.SendTask;        
            case BpmnManualTask:
                return BpmnElementType.ManualTask;
            case BpmnExclusiveGateway:
                return BpmnElementType.ExclusiveGateway;
            case BpmnInclusiveGateway:
                return BpmnElementType.InclusiveGateway;    
            case BpmnParallelGateway:
                return BpmnElementType.ParallelGateway;
            case BpmnEventBasedGateway:
                return BpmnElementType.EventBasedGateway;   
            case BpmnComplexGateway:
                return BpmnElementType.ComplexGateway;
            case BpmnSubProcess:
                return BpmnElementType.SubProcess;
            default:
                return BpmnElementType.Unknown;
        }
    }
}