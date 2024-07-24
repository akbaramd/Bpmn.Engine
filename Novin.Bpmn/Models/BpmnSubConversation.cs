using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("subConversation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnSubConversation : BpmnConversationNode
{
    private BpmnConversationNode[] itemsField;

    /// <remarks/>
    [XmlElement("callConversation", typeof(BpmnCallConversation))]
    [XmlElement("conversation", typeof(BpmnConversation))]
    [XmlElement("conversationNode", typeof(BpmnConversationNode))]
    [XmlElement("subConversation", typeof(BpmnSubConversation))]
    public BpmnConversationNode[] Items
    {
        get { return itemsField; }
        set { itemsField = value; }
    }
}