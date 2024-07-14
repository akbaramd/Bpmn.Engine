namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("outputSet", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable=false)]
public partial class BpmnOutputSet : BpmnBaseElement {
    
    private string[] dataOutputRefsField;
    
    private string[] optionalOutputRefsField;
    
    private string[] whileExecutingOutputRefsField;
    
    private string[] inputSetRefsField;
    
    private string nameField;
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("dataOutputRefs", DataType="IDREF")]
    public string[] dataOutputRefs {
        get {
            return dataOutputRefsField;
        }
        set {
            dataOutputRefsField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("optionalOutputRefs", DataType="IDREF")]
    public string[] optionalOutputRefs {
        get {
            return optionalOutputRefsField;
        }
        set {
            optionalOutputRefsField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("whileExecutingOutputRefs", DataType="IDREF")]
    public string[] whileExecutingOutputRefs {
        get {
            return whileExecutingOutputRefsField;
        }
        set {
            whileExecutingOutputRefsField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("inputSetRefs", DataType="IDREF")]
    public string[] inputSetRefs {
        get {
            return inputSetRefsField;
        }
        set {
            inputSetRefsField = value;
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
}