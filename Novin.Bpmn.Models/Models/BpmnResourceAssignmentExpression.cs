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
[XmlRoot("resourceAssignmentExpression",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnResourceAssignmentExpression : BpmnBaseElement
{
    private BpmnExpression itemField;

    /// <remarks/>
    [XmlElement("expression", typeof(BpmnExpression))]
    [XmlElement("formalExpression", typeof(BpmnFormalExpression))]
    public BpmnExpression Item
    {
        get { return itemField; }
        set { itemField = value; }
    }
}