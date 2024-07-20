namespace Novin.Bpmn.Models;

/// <remarks/>
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnExpression))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnFormalExpression))]
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("baseElementWithMixedContent",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public abstract partial class BpmnBaseElementWithMixedContent
{
    private BpmnDocumentation[] documentationField;

    private BpmnExtensionElements extensionElementsField;

    private string[] textField;

    private string idField;

    private System.Xml.XmlAttribute[] anyAttrField;

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("documentation")]
    public BpmnDocumentation[] documentation
    {
        get { return documentationField; }
        set { documentationField = value; }
    }

    /// <remarks/>
    public BpmnExtensionElements extensionElements
    {
        get { return extensionElementsField; }
        set { extensionElementsField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlTextAttribute()]
    public string[] Text
    {
        get { return textField; }
        set { textField = value; }
    }


    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute(DataType = "ID")]
    public string id
    {
        get { return idField; }
        set { idField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAnyAttributeAttribute()]
    public System.Xml.XmlAttribute[] AnyAttr
    {
        get { return anyAttrField; }
        set { anyAttrField = value; }
    }
}