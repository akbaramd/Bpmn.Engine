namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnGlobalUserTask))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnGlobalScriptTask))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnGlobalManualTask))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnGlobalBusinessRuleTask))]
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("globalTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public partial class BpmnGlobalTask : BpmnCallableElement
{
    private BpmnResourceRole[] itemsField;

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("performer", typeof(BpmnPerformer))]
    [System.Xml.Serialization.XmlElementAttribute("resourceRole", typeof(BpmnResourceRole))]
    public BpmnResourceRole[] Items
    {
        get { return itemsField; }
        set { itemsField = value; }
    }
}