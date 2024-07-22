using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("userTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public partial class BpmnUserTask : BpmnTask
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
    [XmlAttribute()]
    [System.ComponentModel.DefaultValueAttribute("##unspecified")]
    public string implementation
    {
        get { return implementationField; }
        set { implementationField = value; }
    }


    [XmlAttribute("formKey", Namespace = "http://camunda.org/schema/1.0/bpmn")]
    public string FormKey { get; set; }

    [XmlAttribute("candidateGroups", Namespace = "http://camunda.org/schema/1.0/bpmn")]
    public string CandidateGroups { get; set; }
    
    [XmlAttribute("candidateUsers", Namespace = "http://camunda.org/schema/1.0/bpmn")]
    public string CandidateUsers { get; set; }
    
    [XmlAttribute("assignee", Namespace = "http://camunda.org/schema/1.0/bpmn")]
    public string Assignee { get; set; }
}