namespace Novin.Bpmn.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("complexBehaviorDefinition",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public partial class BpmnComplexBehaviorDefinition : BpmnBaseElement
{
    private BpmnFormalExpression conditionField;

    private BpmnImplicitThrowEvent eventField;

    /// <remarks/>
    public BpmnFormalExpression condition
    {
        get { return conditionField; }
        set { conditionField = value; }
    }

    /// <remarks/>
    public BpmnImplicitThrowEvent @event
    {
        get { return eventField; }
        set { eventField = value; }
    }
}