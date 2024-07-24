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
[XmlRoot("startEvent", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnStartEvent : BpmnCatchEvent
{
    private bool isInterruptingField;

    public BpmnStartEvent()
    {
        isInterruptingField = true;
    }

    /// <remarks/>
    [XmlAttribute]
    [DefaultValue(true)]
    public bool isInterrupting
    {
        get { return isInterruptingField; }
        set { isInterruptingField = value; }
    }
}