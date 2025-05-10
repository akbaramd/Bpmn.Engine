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
[XmlRoot("itemDefinition", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnItemDefinition : BpmnRootElement
{
    private XmlQualifiedName structureRefField;

    private bool isCollectionField;

    private BpmnItemKind itemKindField;

    public BpmnItemDefinition()
    {
        isCollectionField = false;
        itemKindField = BpmnItemKind.Information;
    }

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName structureRef
    {
        get { return structureRefField; }
        set { structureRefField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    [DefaultValue(false)]
    public bool isCollection
    {
        get { return isCollectionField; }
        set { isCollectionField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    [DefaultValue(BpmnItemKind.Information)]
    public BpmnItemKind itemKind
    {
        get { return itemKindField; }
        set { itemKindField = value; }
    }
}