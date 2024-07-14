namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("definitions", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable=false)]
public partial class BpmnDefinitions {
    
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
    
    private System.Xml.XmlAttribute[] anyAttrField;
    
    public BpmnDefinitions() {
        expressionLanguageField = "http://www.w3.org/1999/XPath";
        typeLanguageField = "http://www.w3.org/2001/XMLSchema";
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("import")]
    public BpmnImport[] import {
        get {
            return importField;
        }
        set {
            importField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("extension")]
    public BpmnExtension[] extension {
        get {
            return extensionField;
        }
        set {
            extensionField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("category", typeof(BpmnCategory))]
    [System.Xml.Serialization.XmlElementAttribute("collaboration", typeof(BpmnCollaboration))]
    [System.Xml.Serialization.XmlElementAttribute("correlationProperty", typeof(BpmnCorrelationProperty))]
    [System.Xml.Serialization.XmlElementAttribute("dataStore", typeof(BpmnDataStore))]
    [System.Xml.Serialization.XmlElementAttribute("endPoint", typeof(BpmnEndPoint))]
    [System.Xml.Serialization.XmlElementAttribute("error", typeof(BpmnError))]
    [System.Xml.Serialization.XmlElementAttribute("escalation", typeof(BpmnEscalation))]
    [System.Xml.Serialization.XmlElementAttribute("eventDefinition", typeof(BpmnEventDefinition))]
    [System.Xml.Serialization.XmlElementAttribute("globalBusinessRuleTask", typeof(BpmnGlobalBusinessRuleTask))]
    [System.Xml.Serialization.XmlElementAttribute("globalManualTask", typeof(BpmnGlobalManualTask))]
    [System.Xml.Serialization.XmlElementAttribute("globalScriptTask", typeof(BpmnGlobalScriptTask))]
    [System.Xml.Serialization.XmlElementAttribute("globalTask", typeof(BpmnGlobalTask))]
    [System.Xml.Serialization.XmlElementAttribute("globalUserTask", typeof(BpmnGlobalUserTask))]
    [System.Xml.Serialization.XmlElementAttribute("interface", typeof(BpmnInterface))]
    [System.Xml.Serialization.XmlElementAttribute("itemDefinition", typeof(BpmnItemDefinition))]
    [System.Xml.Serialization.XmlElementAttribute("message", typeof(BpmnMessage))]
    [System.Xml.Serialization.XmlElementAttribute("partnerEntity", typeof(BpmnPartnerEntity))]
    [System.Xml.Serialization.XmlElementAttribute("partnerRole", typeof(BpmnPartnerRole))]
    [System.Xml.Serialization.XmlElementAttribute("process", typeof(BpmnProcess))]
    [System.Xml.Serialization.XmlElementAttribute("resource", typeof(BpmnResource))]
    [System.Xml.Serialization.XmlElementAttribute("rootElement", typeof(BpmnRootElement))]
    [System.Xml.Serialization.XmlElementAttribute("signal", typeof(BpmnSignal))]
    public BpmnRootElement[] Items {
        get {
            return itemsField;
        }
        set {
            itemsField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("BPMNDiagram", Namespace="http://www.omg.org/spec/BPMN/20100524/DI")]
    public BPMNDiagram[] BPMNDiagram {
        get {
            return bPMNDiagramField;
        }
        set {
            bPMNDiagramField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("relationship")]
    public BpmnRelationship[] relationship {
        get {
            return relationshipField;
        }
        set {
            relationshipField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute(DataType="ID")]
    public string id {
        get {
            return idField;
        }
        set {
            idField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public string name {
        get {
            return nameField;
        }
        set {
            nameField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute(DataType="anyURI")]
    public string targetNamespace {
        get {
            return targetNamespaceField;
        }
        set {
            targetNamespaceField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute(DataType="anyURI")]
    [System.ComponentModel.DefaultValueAttribute("http://www.w3.org/1999/XPath")]
    public string expressionLanguage {
        get {
            return expressionLanguageField;
        }
        set {
            expressionLanguageField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute(DataType="anyURI")]
    [System.ComponentModel.DefaultValueAttribute("http://www.w3.org/2001/XMLSchema")]
    public string typeLanguage {
        get {
            return typeLanguageField;
        }
        set {
            typeLanguageField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public string exporter {
        get {
            return exporterField;
        }
        set {
            exporterField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public string exporterVersion {
        get {
            return exporterVersionField;
        }
        set {
            exporterVersionField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAnyAttributeAttribute()]
    public System.Xml.XmlAttribute[] AnyAttr {
        get {
            return anyAttrField;
        }
        set {
            anyAttrField = value;
        }
    }
}