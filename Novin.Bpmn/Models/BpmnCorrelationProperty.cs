namespace Novin.Bpmn.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("correlationProperty",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public partial class BpmnCorrelationProperty : BpmnRootElement
{
    private BpmnCorrelationPropertyRetrievalExpression[] correlationPropertyRetrievalExpressionField;

    private string nameField;

    private System.Xml.XmlQualifiedName typeField;

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("correlationPropertyRetrievalExpression")]
    public BpmnCorrelationPropertyRetrievalExpression[] correlationPropertyRetrievalExpression
    {
        get { return correlationPropertyRetrievalExpressionField; }
        set { correlationPropertyRetrievalExpressionField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public string name
    {
        get { return nameField; }
        set { nameField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public System.Xml.XmlQualifiedName type
    {
        get { return typeField; }
        set { typeField = value; }
    }
}