using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks/>
[XmlInclude(typeof(BpmnTimerEventDefinition))]
[XmlInclude(typeof(BpmnTerminateEventDefinition))]
[XmlInclude(typeof(BpmnSignalEventDefinition))]
[XmlInclude(typeof(BpmnMessageEventDefinition))]
[XmlInclude(typeof(BpmnLinkEventDefinition))]
[XmlInclude(typeof(BpmnEscalationEventDefinition))]
[XmlInclude(typeof(BpmnErrorEventDefinition))]
[XmlInclude(typeof(BpmnConditionalEventDefinition))]
[XmlInclude(typeof(BpmnCompensateEventDefinition))]
[XmlInclude(typeof(BpmnCancelEventDefinition))]
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("eventDefinition", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public abstract class BpmnEventDefinition : BpmnRootElement
{
}