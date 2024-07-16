namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("globalUserTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public partial class BpmnGlobalUserTask : BpmnGlobalTask
{
    private BpmnRendering[] renderingField;

    private string implementationField;

    public BpmnGlobalUserTask()
    {
        implementationField = "##unspecified";
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("rendering")]
    public BpmnRendering[] rendering
    {
        get { return renderingField; }
        set { renderingField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    [System.ComponentModel.DefaultValueAttribute("##unspecified")]
    public string implementation
    {
        get { return implementationField; }
        set { implementationField = value; }
    }
}