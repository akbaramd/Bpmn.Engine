namespace Novin.Bpmn.Models;

/// <remarks/>
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnProcess))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnGlobalTask))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnGlobalUserTask))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnGlobalScriptTask))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnGlobalManualTask))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnGlobalBusinessRuleTask))]
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("callableElement", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public partial class BpmnCallableElement : BpmnRootElement
{
    private System.Xml.XmlQualifiedName[] supportedInterfaceRefField;

    private BpmnInputOutputSpecification ioSpecificationField;

    private BpmnInputOutputBinding[] ioBindingField;

    private string nameField;

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("supportedInterfaceRef")]
    public System.Xml.XmlQualifiedName[] supportedInterfaceRef
    {
        get { return supportedInterfaceRefField; }
        set { supportedInterfaceRefField = value; }
    }

    /// <remarks/>
    public BpmnInputOutputSpecification ioSpecification
    {
        get { return ioSpecificationField; }
        set { ioSpecificationField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("ioBinding")]
    public BpmnInputOutputBinding[] ioBinding
    {
        get { return ioBindingField; }
        set { ioBindingField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public string name
    {
        get { return nameField; }
        set { nameField = value; }
    }
}