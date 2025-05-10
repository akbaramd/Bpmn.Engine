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
[XmlRoot("definitions", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnDefinitions
{
    private BpmnImport[] importField;

    private BpmnExtension[] extensionField;

    private BpmnRootElement[] itemsField;

    private BPMNDiagram[] bPMNDiagramField;

    private BpmnRelationship[] relationshipField;

    private string idField;

    private string nameField;

    private string targetNamespaceField;

    private string expressionLanguageField;

    private string typeLanguageField;

    private string exporterField;

    private string exporterVersionField;

    private XmlAttribute[] anyAttrField;

    public BpmnDefinitions()
    {
        expressionLanguageField = "http://www.w3.org/1999/XPath";
        typeLanguageField = "http://www.w3.org/2001/XMLSchema";
    }

    /// <remarks/>
    [XmlElement("import")]
    public BpmnImport[] import
    {
        get { return importField; }
        set { importField = value; }
    }

    /// <remarks/>
    [XmlElement("extension")]
    public BpmnExtension[] extension
    {
        get { return extensionField; }
        set { extensionField = value; }
    }

    /// <remarks/>
    [XmlElement("category", typeof(BpmnCategory))]
    [XmlElement("collaboration", typeof(BpmnCollaboration))]
    [XmlElement("correlationProperty", typeof(BpmnCorrelationProperty))]
    [XmlElement("dataStore", typeof(BpmnDataStore))]
    [XmlElement("endPoint", typeof(BpmnEndPoint))]
    [XmlElement("error", typeof(BpmnError))]
    [XmlElement("escalation", typeof(BpmnEscalation))]
    [XmlElement("eventDefinition", typeof(BpmnEventDefinition))]
    [XmlElement("globalBusinessRuleTask", typeof(BpmnGlobalBusinessRuleTask))]
    [XmlElement("globalManualTask", typeof(BpmnGlobalManualTask))]
    [XmlElement("globalScriptTask", typeof(BpmnGlobalScriptTask))]
    [XmlElement("globalTask", typeof(BpmnGlobalTask))]
    [XmlElement("globalUserTask", typeof(BpmnGlobalUserTask))]
    [XmlElement("interface", typeof(BpmnInterface))]
    [XmlElement("itemDefinition", typeof(BpmnItemDefinition))]
    [XmlElement("message", typeof(BpmnMessage))]
    [XmlElement("partnerEntity", typeof(BpmnPartnerEntity))]
    [XmlElement("partnerRole", typeof(BpmnPartnerRole))]
    [XmlElement("process", typeof(BpmnProcess))]
    [XmlElement("resource", typeof(BpmnResource))]
    [XmlElement("rootElement", typeof(BpmnRootElement))]
    [XmlElement("signal", typeof(BpmnSignal))]
    public BpmnRootElement[] Items
    {
        get { return itemsField; }
        set { itemsField = value; }
    }

    /// <remarks/>
    [XmlElement("BPMNDiagram",
        Namespace = "http://www.omg.org/spec/BPMN/20100524/DI")]
    public BPMNDiagram[] BPMNDiagram
    {
        get { return bPMNDiagramField; }
        set { bPMNDiagramField = value; }
    }

    /// <remarks/>
    [XmlElement("relationship")]
    public BpmnRelationship[] relationship
    {
        get { return relationshipField; }
        set { relationshipField = value; }
    }

    /// <remarks/>
    [XmlAttribute(DataType = "ID")]
    public string id
    {
        get { return idField; }
        set { idField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string name
    {
        get { return nameField; }
        set { nameField = value; }
    }

    /// <remarks/>
    [XmlAttribute(DataType = "anyURI")]
    public string targetNamespace
    {
        get { return targetNamespaceField; }
        set { targetNamespaceField = value; }
    }

    /// <remarks/>
    [XmlAttribute(DataType = "anyURI")]
    [DefaultValue("http://www.w3.org/1999/XPath")]
    public string expressionLanguage
    {
        get { return expressionLanguageField; }
        set { expressionLanguageField = value; }
    }

    /// <remarks/>
    [XmlAttribute(DataType = "anyURI")]
    [DefaultValue("http://www.w3.org/2001/XMLSchema")]
    public string typeLanguage
    {
        get { return typeLanguageField; }
        set { typeLanguageField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string exporter
    {
        get { return exporterField; }
        set { exporterField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string exporterVersion
    {
        get { return exporterVersionField; }
        set { exporterVersionField = value; }
    }

    /// <remarks/>
    [XmlAnyAttribute]
    public XmlAttribute[] AnyAttr
    {
        get { return anyAttrField; }
        set { anyAttrField = value; }
    }
}