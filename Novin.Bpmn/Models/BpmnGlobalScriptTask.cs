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
[XmlRoot("globalScriptTask",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnGlobalScriptTask : BpmnGlobalTask
{
    private XmlNode scriptField;

    private string scriptLanguageField;

    /// <remarks/>
    public XmlNode script
    {
        get { return scriptField; }
        set { scriptField = value; }
    }

    /// <remarks/>
    [XmlAttribute(DataType = "anyURI")]
    public string scriptLanguage
    {
        get { return scriptLanguageField; }
        set { scriptLanguageField = value; }
    }
}