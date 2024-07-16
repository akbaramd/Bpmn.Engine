namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("subConversation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public partial class BpmnSubConversation : BpmnConversationNode
{
    private BpmnConversationNode[] itemsField;

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("callConversation", typeof(BpmnCallConversation))]
    [System.Xml.Serialization.XmlElementAttribute("conversation", typeof(BpmnConversation))]
    [System.Xml.Serialization.XmlElementAttribute("conversationNode", typeof(BpmnConversationNode))]
    [System.Xml.Serialization.XmlElementAttribute("subConversation", typeof(BpmnSubConversation))]
    public BpmnConversationNode[] Items
    {
        get { return itemsField; }
        set { itemsField = value; }
    }
}