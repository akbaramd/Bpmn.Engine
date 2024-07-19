using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test.Abstractions;

public interface IBpmnFileDeserializer
{
    BpmnDefinitions Deserialize(string filePath);
}