using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks />
[XmlInclude(typeof(BpmnUserTask))]
[XmlInclude(typeof(BpmnServiceTask))]
[XmlInclude(typeof(BpmnSendTask))]
[XmlInclude(typeof(BpmnScriptTask))]
[XmlInclude(typeof(BpmnReceiveTask))]
[XmlInclude(typeof(BpmnManualTask))]
[XmlInclude(typeof(BpmnBusinessRuleTask))]
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("task", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnTask : BpmnActivity
{
}