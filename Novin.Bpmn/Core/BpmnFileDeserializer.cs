using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Models;

namespace Novin.Bpmn.Core;

public class BpmnFileDeserializer : IBpmnFileDeserializer
{
    public BpmnDefinitions DeserializeFromPath(string filePath)
    {
        var xmlContent = System.IO.File.ReadAllText(filePath);
        var serializer = new System.Xml.Serialization.XmlSerializer(typeof(BpmnDefinitions),
            "http://www.omg.org/spec/BPMN/20100524/MODEL");
        using var stringReader = new System.IO.StringReader(xmlContent);
        return (BpmnDefinitions)serializer.Deserialize(stringReader)!;
    }
    
    public BpmnDefinitions Deserialize(string content)
    {
        var serializer = new System.Xml.Serialization.XmlSerializer(typeof(BpmnDefinitions),
            "http://www.omg.org/spec/BPMN/20100524/MODEL");
        using var stringReader = new System.IO.StringReader(content);
        return (BpmnDefinitions)serializer.Deserialize(stringReader)!;
    }
}