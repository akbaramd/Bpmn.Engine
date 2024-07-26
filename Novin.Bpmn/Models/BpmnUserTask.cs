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
[XmlRoot("userTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnUserTask : BpmnTask
{
    private BpmnRendering[] renderingField;

    private string implementationField;

    public BpmnUserTask()
    {
        implementationField = "##unspecified";
    }

    /// <remarks/>
    [XmlElement("rendering")]
    public BpmnRendering[] rendering
    {
        get { return renderingField; }
        set { renderingField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    [DefaultValue("##unspecified")]
    public string implementation
    {
        get { return implementationField; }
        set { implementationField = value; }
    }


    [XmlAttribute("formId")]
    public string formId { get; set; }

    [XmlAttribute("candidateGroups",AttributeName = "candidateGroups")]
    public string candidateGroups { get; set; }
    
    [XmlAttribute("candidateUsers",AttributeName = "candidateUsers")]
    public string candidateUsers { get; set; }
    
    [XmlAttribute("assignee",AttributeName = "assignee")]
    public string assignee { get; set; }
    

}