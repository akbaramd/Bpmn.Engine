using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks/>
[XmlInclude(typeof(BpmnTextAnnotation))]
[XmlInclude(typeof(BpmnGroup))]
[XmlInclude(typeof(BpmnAssociation))]
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("artifact", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public abstract class BpmnArtifact : BpmnBaseElement
{
}