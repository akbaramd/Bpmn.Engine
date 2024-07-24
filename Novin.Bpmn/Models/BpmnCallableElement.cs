using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks/>
[XmlInclude(typeof(BpmnProcess))]
[XmlInclude(typeof(BpmnGlobalTask))]
[XmlInclude(typeof(BpmnGlobalUserTask))]
[XmlInclude(typeof(BpmnGlobalScriptTask))]
[XmlInclude(typeof(BpmnGlobalManualTask))]
[XmlInclude(typeof(BpmnGlobalBusinessRuleTask))]
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("callableElement", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnCallableElement : BpmnRootElement
{
    private XmlQualifiedName[] supportedInterfaceRefField;

    private BpmnInputOutputSpecification ioSpecificationField;

    private BpmnInputOutputBinding[] ioBindingField;

    private string nameField;

    /// <remarks/>
    [XmlElement("supportedInterfaceRef")]
    public XmlQualifiedName[] supportedInterfaceRef
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
    [XmlElement("ioBinding")]
    public BpmnInputOutputBinding[] ioBinding
    {
        get { return ioBindingField; }
        set { ioBindingField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string name
    {
        get { return nameField; }
        set { nameField = value; }
    }
}