﻿using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Novin.Bpmn.Test.Models;

/// <remarks />
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("businessRuleTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnBusinessRuleTask : BpmnTask
{
    private string implementationField;

    public BpmnBusinessRuleTask()
    {
        implementationField = "##unspecified";
    }

    /// <remarks />
    [XmlAttribute]
    [DefaultValue("##unspecified")]
    public string implementation
    {
        get => implementationField;
        set => implementationField = value;
    }
}