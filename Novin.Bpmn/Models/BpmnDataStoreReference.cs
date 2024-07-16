namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("dataStoreReference",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public partial class BpmnDataStoreReference : BpmnFlowElement
{
    private BpmnDataState dataStateField;

    private System.Xml.XmlQualifiedName itemSubjectRefField;

    private System.Xml.XmlQualifiedName dataStoreRefField;

    /// <remarks/>
    public BpmnDataState dataState
    {
        get { return dataStateField; }
        set { dataStateField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public System.Xml.XmlQualifiedName itemSubjectRef
    {
        get { return itemSubjectRefField; }
        set { itemSubjectRefField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public System.Xml.XmlQualifiedName dataStoreRef
    {
        get { return dataStoreRefField; }
        set { dataStoreRefField = value; }
    }
}