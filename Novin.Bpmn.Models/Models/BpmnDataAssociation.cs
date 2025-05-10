using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks/>
[XmlInclude(typeof(BpmnDataOutputAssociation))]
[XmlInclude(typeof(BpmnDataInputAssociation))]
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("dataAssociation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnDataAssociation : BpmnBaseElement
{
    private string[] sourceRefField;

    private string targetRefField;

    private BpmnFormalExpression transformationField;

    private BpmnAssignment[] assignmentField;

    /// <remarks/>
    [XmlElement("sourceRef", DataType = "IDREF")]
    public string[] sourceRef
    {
        get { return sourceRefField; }
        set { sourceRefField = value; }
    }

    /// <remarks/>
    [XmlElement(DataType = "IDREF")]
    public string targetRef
    {
        get { return targetRefField; }
        set { targetRefField = value; }
    }

    /// <remarks/>
    public BpmnFormalExpression transformation
    {
        get { return transformationField; }
        set { transformationField = value; }
    }

    /// <remarks/>
    [XmlElement("assignment")]
    public BpmnAssignment[] assignment
    {
        get { return assignmentField; }
        set { assignmentField = value; }
    }
}