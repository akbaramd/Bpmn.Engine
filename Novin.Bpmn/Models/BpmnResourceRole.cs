namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnPerformer))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnHumanPerformer))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnPotentialOwner))]
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("resourceRole", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public partial class BpmnResourceRole : BpmnBaseElement
{
    private object[] itemsField;

    private string nameField;

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("resourceAssignmentExpression",
        typeof(BpmnResourceAssignmentExpression))]
    [System.Xml.Serialization.XmlElementAttribute("resourceParameterBinding", typeof(BpmnResourceParameterBinding))]
    [System.Xml.Serialization.XmlElementAttribute("resourceRef", typeof(System.Xml.XmlQualifiedName))]
    public object[] Items
    {
        get { return itemsField; }
        set { itemsField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public string name
    {
        get { return nameField; }
        set { nameField = value; }
    }
}