using System.CodeDom.Compiler;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks />
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public enum BpmnEventBasedGatewayType
{
    /// <remarks />
    Exclusive,

    /// <remarks />
    Parallel
}