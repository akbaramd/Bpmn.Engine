using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks/>
[XmlInclude(typeof(BpmnExpression))]
[XmlInclude(typeof(BpmnFormalExpression))]
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("baseElementWithMixedContent",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public abstract class BpmnBaseElementWithMixedContent
{
    private BpmnDocumentation[] documentationField;

    private BpmnExtensionElements extensionElementsField;

    private string[] textField;

    private string idField;

    private XmlAttribute[] anyAttrField;

    /// <remarks/>
    [XmlElement("documentation")]
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
    [XmlText]
    public string[] Text
    {
        get { return textField; }
        set { textField = value; }
    }


    /// <remarks/>
    [XmlAttribute(DataType = "ID")]
    public string id
    {
        get { return idField; }
        set { idField = value; }
    }

    /// <remarks/>
    [XmlAnyAttribute]
    public XmlAttribute[] AnyAttr
    {
        get { return anyAttrField; }
        set { anyAttrField = value; }
    }
}