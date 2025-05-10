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
[XmlRoot("correlationPropertyBinding",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnCorrelationPropertyBinding : BpmnBaseElement
{
    private BpmnFormalExpression dataPathField;

    private XmlQualifiedName correlationPropertyRefField;

    /// <remarks/>
    public BpmnFormalExpression dataPath
    {
        get { return dataPathField; }
        set { dataPathField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName correlationPropertyRef
    {
        get { return correlationPropertyRefField; }
        set { correlationPropertyRefField = value; }
    }
}