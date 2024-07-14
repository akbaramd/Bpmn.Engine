namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace="http://www.omg.org/spec/DD/20100524/DC")]
[System.Xml.Serialization.XmlRootAttribute(Namespace="http://www.omg.org/spec/DD/20100524/DC", IsNullable=false)]
public partial class Bounds {
    
    private double xField;
    
    private double yField;
    
    private double widthField;
    
    private double heightField;
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public double x {
        get {
            return xField;
        }
        set {
            xField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public double y {
        get {
            return yField;
        }
        set {
            yField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public double width {
        get {
            return widthField;
        }
        set {
            widthField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public double height {
        get {
            return heightField;
        }
        set {
            heightField = value;
        }
    }
}