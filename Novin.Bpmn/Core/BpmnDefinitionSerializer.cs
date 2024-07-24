using System.Xml.Serialization;
using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Models;

namespace Novin.Bpmn.Core;

public static class BpmnDefinitionSerializer 
{
    public static BpmnDefinitions DeserializeFromPath(string filePath)
    {
        var xmlContent = File.ReadAllText(filePath);
        var serializer = new XmlSerializer(typeof(BpmnDefinitions),
            "http://www.omg.org/spec/BPMN/20100524/MODEL");
        using var stringReader = new StringReader(xmlContent);
        return (BpmnDefinitions)serializer.Deserialize(stringReader)!;
    }
    
    public static BpmnDefinitions Deserialize(string content)
    {
        var serializer = new XmlSerializer(typeof(BpmnDefinitions),
            "http://www.omg.org/spec/BPMN/20100524/MODEL");
        using var stringReader = new StringReader(content);
        return (BpmnDefinitions)serializer.Deserialize(stringReader)!;
    }
}