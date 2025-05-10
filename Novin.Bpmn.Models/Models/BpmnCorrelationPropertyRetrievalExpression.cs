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
[XmlRoot("correlationPropertyRetrievalExpression",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnCorrelationPropertyRetrievalExpression : BpmnBaseElement
{
    private BpmnFormalExpression messagePathField;

    private XmlQualifiedName messageRefField;

    /// <remarks/>
    public BpmnFormalExpression messagePath
    {
        get { return messagePathField; }
        set { messagePathField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName messageRef
    {
        get { return messageRefField; }
        set { messageRefField = value; }
    }
}