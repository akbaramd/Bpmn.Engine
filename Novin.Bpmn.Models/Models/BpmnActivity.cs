#nullable disable
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks/>
[XmlInclude(typeof(BpmnTask))]
[XmlInclude(typeof(BpmnUserTask))]
[XmlInclude(typeof(BpmnServiceTask))]
[XmlInclude(typeof(BpmnSendTask))]
[XmlInclude(typeof(BpmnScriptTask))]
[XmlInclude(typeof(BpmnReceiveTask))]
[XmlInclude(typeof(BpmnManualTask))]
[XmlInclude(typeof(BpmnBusinessRuleTask))]
[XmlInclude(typeof(BpmnSubProcess))]
[XmlInclude(typeof(BpmnTransaction))]
[XmlInclude(typeof(BpmnAdHocSubProcess))]
[XmlInclude(typeof(BpmnCallActivity))]
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("activity", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public abstract class BpmnActivity : BpmnFlowNode
{
    private BpmnInputOutputSpecification ioSpecificationField;

    private BpmnProperty[] propertyField;

    private BpmnDataInputAssociation[] dataInputAssociationField;

    private BpmnDataOutputAssociation[] dataOutputAssociationField;

    private BpmnResourceRole[] itemsField;

    private BpmnLoopCharacteristics itemField;

    private bool isForCompensationField;

    private string startQuantityField;

    private string completionQuantityField;

    private string defaultField;

    public BpmnActivity()
    {
        isForCompensationField = false;
        startQuantityField = "1";
        completionQuantityField = "1";
    }

    /// <remarks/>
    public BpmnInputOutputSpecification ioSpecification
    {
        get { return ioSpecificationField; }
        set { ioSpecificationField = value; }
    }

    /// <remarks/>
    [XmlElement("property")]
    public BpmnProperty[] property
    {
        get { return propertyField; }
        set { propertyField = value; }
    }

    /// <remarks/>
    [XmlElement("dataInputAssociation")]
    public BpmnDataInputAssociation[] dataInputAssociation
    {
        get { return dataInputAssociationField; }
        set { dataInputAssociationField = value; }
    }

    /// <remarks/>
    [XmlElement("dataOutputAssociation")]
    public BpmnDataOutputAssociation[] dataOutputAssociation
    {
        get { return dataOutputAssociationField; }
        set { dataOutputAssociationField = value; }
    }

    /// <remarks/>
    [XmlElement("performer", typeof(BpmnPerformer))]
    [XmlElement("resourceRole", typeof(BpmnResourceRole))]
    public BpmnResourceRole[] Items
    {
        get { return itemsField; }
        set { itemsField = value; }
    }

    /// <remarks/>
    [XmlElement("loopCharacteristics", typeof(BpmnLoopCharacteristics))]
    [XmlElement("multiInstanceLoopCharacteristics",
        typeof(BpmnMultiInstanceLoopCharacteristics))]
    [XmlElement("standardLoopCharacteristics",
        typeof(BpmnStandardLoopCharacteristics))]
    public BpmnLoopCharacteristics Item
    {
        get { return itemField; }
        set { itemField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    [DefaultValue(false)]
    public bool isForCompensation
    {
        get { return isForCompensationField; }
        set { isForCompensationField = value; }
    }

    /// <remarks/>
    [XmlAttribute(DataType = "integer")]
    [DefaultValue("1")]
    public string startQuantity
    {
        get { return startQuantityField; }
        set { startQuantityField = value; }
    }

    /// <remarks/>
    [XmlAttribute(DataType = "integer")]
    [DefaultValue("1")]
    public string completionQuantity
    {
        get { return completionQuantityField; }
        set { completionQuantityField = value; }
    }

    /// <remarks/>
    [XmlAttribute(DataType = "IDREF")]
    public string @default
    {
        get { return defaultField; }
        set { defaultField = value; }
    }
}