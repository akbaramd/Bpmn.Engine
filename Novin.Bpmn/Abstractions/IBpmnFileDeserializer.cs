using Novin.Bpmn.Models;

namespace Novin.Bpmn.Abstractions;

public interface IBpmnFileDeserializer
{
    BpmnDefinitions Deserialize(string filePath);
}