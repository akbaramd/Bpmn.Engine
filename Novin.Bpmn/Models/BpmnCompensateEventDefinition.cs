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
[XmlRoot("compensateEventDefinition",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnCompensateEventDefinition : BpmnEventDefinition
{
    private bool waitForCompletionField;

    private bool waitForCompletionFieldSpecified;

    private XmlQualifiedName activityRefField;

    /// <remarks/>
    [XmlAttribute]
    public bool waitForCompletion
    {
        get { return waitForCompletionField; }
        set { waitForCompletionField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool waitForCompletionSpecified
    {
        get { return waitForCompletionFieldSpecified; }
        set { waitForCompletionFieldSpecified = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName activityRef
    {
        get { return activityRefField; }
        set { activityRefField = value; }
    }
}