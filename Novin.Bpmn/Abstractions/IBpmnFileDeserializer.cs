using Novin.Bpmn.Models;

namespace Novin.Bpmn.Abstractions;

public interface IBpmnFileDeserializer
{
    BpmnDefinitions DeserializeFromPath(string filePath);
    BpmnDefinitions Deserialize(string content);
}