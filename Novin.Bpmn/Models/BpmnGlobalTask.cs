using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks/>
[XmlInclude(typeof(BpmnGlobalUserTask))]
[XmlInclude(typeof(BpmnGlobalScriptTask))]
[XmlInclude(typeof(BpmnGlobalManualTask))]
[XmlInclude(typeof(BpmnGlobalBusinessRuleTask))]
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("globalTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnGlobalTask : BpmnCallableElement
{
    private BpmnResourceRole[] itemsField;

    /// <remarks/>
    [XmlElement("performer", typeof(BpmnPerformer))]
    [XmlElement("resourceRole", typeof(BpmnResourceRole))]
    public BpmnResourceRole[] Items
    {
        get { return itemsField; }
        set { itemsField = value; }
    }
}