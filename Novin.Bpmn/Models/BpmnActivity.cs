#nullable disable
namespace Novin.Bpmn.Models;

/// <remarks/>
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnTask))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnUserTask))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnServiceTask))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnSendTask))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnScriptTask))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnReceiveTask))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnManualTask))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnBusinessRuleTask))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnSubProcess))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnTransaction))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnAdHocSubProcess))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnCallActivity))]
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("activity", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public abstract partial class BpmnActivity : BpmnFlowNode
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
    [System.Xml.Serialization.XmlElementAttribute("property")]
    public BpmnProperty[] property
    {
        get { return propertyField; }
        set { propertyField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("dataInputAssociation")]
    public BpmnDataInputAssociation[] dataInputAssociation
    {
        get { return dataInputAssociationField; }
        set { dataInputAssociationField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("dataOutputAssociation")]
    public BpmnDataOutputAssociation[] dataOutputAssociation
    {
        get { return dataOutputAssociationField; }
        set { dataOutputAssociationField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("performer", typeof(BpmnPerformer))]
    [System.Xml.Serialization.XmlElementAttribute("resourceRole", typeof(BpmnResourceRole))]
    public BpmnResourceRole[] Items
    {
        get { return itemsField; }
        set { itemsField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("loopCharacteristics", typeof(BpmnLoopCharacteristics))]
    [System.Xml.Serialization.XmlElementAttribute("multiInstanceLoopCharacteristics",
        typeof(BpmnMultiInstanceLoopCharacteristics))]
    [System.Xml.Serialization.XmlElementAttribute("standardLoopCharacteristics",
        typeof(BpmnStandardLoopCharacteristics))]
    public BpmnLoopCharacteristics Item
    {
        get { return itemField; }
        set { itemField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    [System.ComponentModel.DefaultValueAttribute(false)]
    public bool isForCompensation
    {
        get { return isForCompensationField; }
        set { isForCompensationField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute(DataType = "integer")]
    [System.ComponentModel.DefaultValueAttribute("1")]
    public string startQuantity
    {
        get { return startQuantityField; }
        set { startQuantityField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute(DataType = "integer")]
    [System.ComponentModel.DefaultValueAttribute("1")]
    public string completionQuantity
    {
        get { return completionQuantityField; }
        set { completionQuantityField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute(DataType = "IDREF")]
    public string @default
    {
        get { return defaultField; }
        set { defaultField = value; }
    }
}