﻿namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("receiveTask", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable=false)]
public partial class BpmnReceiveTask : BpmnTask {
    
    private string implementationField;
    
    private bool instantiateField;
    
    private System.Xml.XmlQualifiedName messageRefField;
    
    private System.Xml.XmlQualifiedName operationRefField;
    
    public BpmnReceiveTask() {
        implementationField = "##WebService";
        instantiateField = false;
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    [System.ComponentModel.DefaultValueAttribute("##WebService")]
    public string implementation {
        get {
            return implementationField;
        }
        set {
            implementationField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    [System.ComponentModel.DefaultValueAttribute(false)]
    public bool instantiate {
        get {
            return instantiateField;
        }
        set {
            instantiateField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public System.Xml.XmlQualifiedName messageRef {
        get {
            return messageRefField;
        }
        set {
            messageRefField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public System.Xml.XmlQualifiedName operationRef {
        get {
            return operationRefField;
        }
        set {
            operationRefField = value;
        }
    }
}