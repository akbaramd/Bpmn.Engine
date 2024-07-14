namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("dataStore", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable=false)]
public partial class BpmnDataStore : BpmnRootElement {
    
    private BpmnDataState dataStateField;
    
    private string nameField;
    
    private string capacityField;
    
    private bool isUnlimitedField;
    
    private System.Xml.XmlQualifiedName itemSubjectRefField;
    
    public BpmnDataStore() {
        isUnlimitedField = true;
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
    public string name {
        get {
            return nameField;
        }
        set {
            nameField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute(DataType="integer")]
    public string capacity {
        get {
            return capacityField;
        }
        set {
            capacityField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    [System.ComponentModel.DefaultValueAttribute(true)]
    public bool isUnlimited {
        get {
            return isUnlimitedField;
        }
        set {
            isUnlimitedField = value;
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
}