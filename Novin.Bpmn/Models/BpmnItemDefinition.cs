namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("itemDefinition", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public partial class BpmnItemDefinition : BpmnRootElement
{
    private System.Xml.XmlQualifiedName structureRefField;

    private bool isCollectionField;

    private BpmnItemKind itemKindField;

    public BpmnItemDefinition()
    {
        isCollectionField = false;
        itemKindField = BpmnItemKind.Information;
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public System.Xml.XmlQualifiedName structureRef
    {
        get { return structureRefField; }
        set { structureRefField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    [System.ComponentModel.DefaultValueAttribute(false)]
    public bool isCollection
    {
        get { return isCollectionField; }
        set { isCollectionField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    [System.ComponentModel.DefaultValueAttribute(BpmnItemKind.Information)]
    public BpmnItemKind itemKind
    {
        get { return itemKindField; }
        set { itemKindField = value; }
    }
}