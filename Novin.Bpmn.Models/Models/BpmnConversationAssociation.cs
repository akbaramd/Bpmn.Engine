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
[XmlRoot("conversationAssociation",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnConversationAssociation : BpmnBaseElement
{
    private XmlQualifiedName innerConversationNodeRefField;

    private XmlQualifiedName outerConversationNodeRefField;

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName innerConversationNodeRef
    {
        get { return innerConversationNodeRefField; }
        set { innerConversationNodeRefField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName outerConversationNodeRef
    {
        get { return outerConversationNodeRefField; }
        set { outerConversationNodeRefField = value; }
    }
}