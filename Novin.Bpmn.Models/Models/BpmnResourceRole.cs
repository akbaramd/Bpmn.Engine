using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks/>
[XmlInclude(typeof(BpmnPerformer))]
[XmlInclude(typeof(BpmnHumanPerformer))]
[XmlInclude(typeof(BpmnPotentialOwner))]
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("resourceRole", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnResourceRole : BpmnBaseElement
{
    private object[] itemsField;

    private string nameField;

    /// <remarks/>
    [XmlElement("resourceAssignmentExpression",
        typeof(BpmnResourceAssignmentExpression))]
    [XmlElement("resourceParameterBinding", typeof(BpmnResourceParameterBinding))]
    [XmlElement("resourceRef", typeof(XmlQualifiedName))]
    public object[] Items
    {
        get { return itemsField; }
        set { itemsField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string name
    {
        get { return nameField; }
        set { nameField = value; }
    }
}