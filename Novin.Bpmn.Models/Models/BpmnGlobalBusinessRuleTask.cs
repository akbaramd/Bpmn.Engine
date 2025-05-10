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
[XmlRoot("globalBusinessRuleTask",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnGlobalBusinessRuleTask : BpmnGlobalTask
{
    private string implementationField;

    public BpmnGlobalBusinessRuleTask()
    {
        implementationField = "##unspecified";
    }

    /// <remarks/>
    [XmlAttribute]
    [DefaultValue("##unspecified")]
    public string implementation
    {
        get { return implementationField; }
        set { implementationField = value; }
    }
}