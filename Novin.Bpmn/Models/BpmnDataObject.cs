namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("dataObject", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable=false)]
public partial class BpmnDataObject : BpmnFlowElement {
    
    private BpmnDataState dataStateField;
    
    private System.Xml.XmlQualifiedName itemSubjectRefField;
    
    private bool isCollectionField;
    
    public BpmnDataObject() {
        isCollectionField = false;
    }
    
    /// <remarks/>
    public BpmnDataState dataState {
        get {
            return dataStateField;
        }
        set {
            dataStateField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public System.Xml.XmlQualifiedName itemSubjectRef {
        get {
            return itemSubjectRefField;
        }
        set {
            itemSubjectRefField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    [System.ComponentModel.DefaultValueAttribute(false)]
    public bool isCollection {
        get {
            return isCollectionField;
        }
        set {
            isCollectionField = value;
        }
    }
}