namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("globalScriptTask", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable=false)]
public partial class BpmnGlobalScriptTask : BpmnGlobalTask {
    
    private System.Xml.XmlNode scriptField;
    
    private string scriptLanguageField;
    
    /// <remarks/>
    public System.Xml.XmlNode script {
        get {
            return scriptField;
        }
        set {
            scriptField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute(DataType="anyURI")]
    public string scriptLanguage {
        get {
            return scriptLanguageField;
        }
        set {
            scriptLanguageField = value;
        }
    }
}