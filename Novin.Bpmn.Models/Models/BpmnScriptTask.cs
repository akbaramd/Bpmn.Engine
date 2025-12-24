using System;
using System.Collections.Generic;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models.Models
{
    public static class BpmnXmlNamespaces
    {
        public const string Bpmn  = "http://www.omg.org/spec/BPMN/20100524/MODEL";
        public const string Zeebe = "http://camunda.org/schema/zeebe/1.0";
        public const string Bonyan = "http://bonyan.org/schema/bpmn/1.0";
        // سایر NS ها …
    }
    /* =======================================================================
     * ZEEBE EXTENSION ELEMENTS
     * =====================================================================*/
    [GeneratedCode("xsd", "4.8.3928.0")]
    [Serializable, DebuggerStepThrough, DesignerCategory("code")]
    [XmlType("ioMapping", Namespace = BpmnXmlNamespaces.Zeebe)]
    public class ZeebeIoMapping
    {
        [XmlElement("input",  Namespace = BpmnXmlNamespaces.Zeebe)]
        public List<ZeebeIoMappingInput> Inputs  { get; set; } = new();

        [XmlElement("output", Namespace = BpmnXmlNamespaces.Zeebe)]
        public List<ZeebeIoMappingOutput> Outputs { get; set; } = new();
    }

    [GeneratedCode("xsd", "4.8.3928.0")]
    [Serializable, DebuggerStepThrough, DesignerCategory("code")]
    [XmlType("input", Namespace = BpmnXmlNamespaces.Zeebe)]
    public class ZeebeIoMappingInput
    {
        [XmlAttribute("source")] public string Source { get; set; } = null!;
        [XmlAttribute("target")] public string Target { get; set; } = null!;
    }

    [GeneratedCode("xsd", "4.8.3928.0")]
    [Serializable, DebuggerStepThrough, DesignerCategory("code")]
    [XmlType("output", Namespace = BpmnXmlNamespaces.Zeebe)]
    public class ZeebeIoMappingOutput
    {
        [XmlAttribute("source")] public string Source { get; set; } = null!;
        [XmlAttribute("target")] public string Target { get; set; } = null!;
    }

    [GeneratedCode("xsd", "4.8.3928.0")]
    [Serializable, DebuggerStepThrough, DesignerCategory("code")]
    [XmlType("script", Namespace = BpmnXmlNamespaces.Zeebe)]
    public class ZeebeScript
    {
        [XmlAttribute("expression")]     public string Expression     { get; set; } = null!;
        [XmlAttribute("resultVariable")] public string ResultVariable { get; set; } = null!;
    }


    /* =======================================================================
     * BPMN SCRIPT TASK  (با پشتیبانی Zeebe)
     * =====================================================================*/
    [GeneratedCode("xsd", "4.8.3928.0")]
    [Serializable, DebuggerStepThrough, DesignerCategory("code")]
    [XmlType(
        "scriptTask",
        Namespace = BpmnNs)]
    [XmlRoot("scriptTask", Namespace = BpmnNs, IsNullable = false)]
    public class BpmnScriptTask : BpmnTask
    {
        // --- فیلد کلاسیک Camunda-7 (bpmn:script) ----------------------------
        private XmlNode? scriptField;

        [XmlElement("script", Namespace = BpmnNs)]       // <bpmn:script>
        public XmlNode? Script
        {
            get => scriptField;
            set => scriptField = value;
        }

        // --- صفت اختیاری استاندارد -----------------------------------------
        [XmlAttribute("scriptFormat")]
        public string? ScriptFormat { get; set; }

        /* -------------------------------------------------------------------
         * ZEEBE EXTENSIONS
         * -----------------------------------------------------------------*/
        [XmlElement("ioMapping", Namespace = ZeebeNs)]
        public ZeebeIoMapping? IoMapping { get; set; }

        [XmlElement("script", Namespace = ZeebeNs)]      // <zeebe:script …/>
        public ZeebeScript? ZeebeScript { get; set; }



        [XmlElement("script", Namespace = BonyanNs)]      // <bonyan:script>
        public BonyanScript? BonyanScript { get; set; }

        /* -------------------------------------------------------------------
         * ثابت‌های فضای نام
         * -----------------------------------------------------------------*/
        public const string BpmnNs  = "http://www.omg.org/spec/BPMN/20100524/MODEL";
        public const string ZeebeNs = "http://camunda.org/schema/zeebe/1.0";
        public const string BonyanNs = "http://bonyan.org/schema/bpmn/1.0";
    }
}
