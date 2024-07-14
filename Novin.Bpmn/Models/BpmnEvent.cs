namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnThrowEvent))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnIntermediateThrowEvent))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnImplicitThrowEvent))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnEndEvent))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnCatchEvent))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnStartEvent))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnIntermediateCatchEvent))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnBoundaryEvent))]
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("event", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable=false)]
public abstract partial class BpmnEvent : BpmnFlowNode {
    
    private BpmnProperty[] propertyField;
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("property")]
    public BpmnProperty[] property {
        get {
            return propertyField;
        }
        set {
            propertyField = value;
        }
    }
}