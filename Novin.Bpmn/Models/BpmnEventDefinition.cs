namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnTimerEventDefinition))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnTerminateEventDefinition))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnSignalEventDefinition))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnMessageEventDefinition))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnLinkEventDefinition))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnEscalationEventDefinition))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnErrorEventDefinition))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnConditionalEventDefinition))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnCompensateEventDefinition))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnCancelEventDefinition))]
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("eventDefinition", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable=false)]
public abstract partial class BpmnEventDefinition : BpmnRootElement {
}