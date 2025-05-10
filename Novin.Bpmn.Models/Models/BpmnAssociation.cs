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
[XmlRoot("association", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnAssociation : BpmnArtifact
{
    private XmlQualifiedName sourceRefField;

    private XmlQualifiedName targetRefField;

    private BpmnAssociationDirection associationDirectionField;

    public BpmnAssociation()
    {
        associationDirectionField = BpmnAssociationDirection.None;
    }

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName sourceRef
    {
        get { return sourceRefField; }
        set { sourceRefField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName targetRef
    {
        get { return targetRefField; }
        set { targetRefField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    [DefaultValue(BpmnAssociationDirection.None)]
    public BpmnAssociationDirection associationDirection
    {
        get { return associationDirectionField; }
        set { associationDirectionField = value; }
    }
}