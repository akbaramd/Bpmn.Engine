using System.CodeDom.Compiler;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/DI")]
public enum ParticipantBandKind
{
    /// <remarks/>
    top_initiating,

    /// <remarks/>
    middle_initiating,

    /// <remarks/>
    bottom_initiating,

    /// <remarks/>
    top_non_initiating,

    /// <remarks/>
    middle_non_initiating,

    /// <remarks/>
    bottom_non_initiating,
}