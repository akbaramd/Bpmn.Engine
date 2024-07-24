using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("resourceParameterBinding",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnResourceParameterBinding : BpmnBaseElement
{
    private BpmnExpression itemField;

    private XmlQualifiedName parameterRefField;

    /// <remarks/>
    [XmlElement("expression", typeof(BpmnExpression))]
    [XmlElement("formalExpression", typeof(BpmnFormalExpression))]
    public BpmnExpression Item
    {
        get { return itemField; }
        set { itemField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName parameterRef
    {
        get { return parameterRefField; }
        set { parameterRefField = value; }
    }
}