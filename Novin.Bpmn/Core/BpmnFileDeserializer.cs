using Novin.Bpmn.Test.Abstractions;
using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test.Core;

public class BpmnFileDeserializer : IBpmnFileDeserializer
{
    public BpmnDefinitions Deserialize(string filePath)
    {
        var xmlContent = System.IO.File.ReadAllText(filePath);
        var serializer = new System.Xml.Serialization.XmlSerializer(typeof(BpmnDefinitions),
            "http://www.omg.org/spec/BPMN/20100524/MODEL");
        using var stringReader = new System.IO.StringReader(xmlContent);
        return (BpmnDefinitions)serializer.Deserialize(stringReader)!;
    }
}