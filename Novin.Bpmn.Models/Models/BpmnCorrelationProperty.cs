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
[XmlRoot("correlationProperty",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnCorrelationProperty : BpmnRootElement
{
    private BpmnCorrelationPropertyRetrievalExpression[] correlationPropertyRetrievalExpressionField;

    private string nameField;

    private XmlQualifiedName typeField;

    /// <remarks/>
    [XmlElement("correlationPropertyRetrievalExpression")]
    public BpmnCorrelationPropertyRetrievalExpression[] correlationPropertyRetrievalExpression
    {
        get { return correlationPropertyRetrievalExpressionField; }
        set { correlationPropertyRetrievalExpressionField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string name
    {
        get { return nameField; }
        set { nameField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName type
    {
        get { return typeField; }
        set { typeField = value; }
    }
}