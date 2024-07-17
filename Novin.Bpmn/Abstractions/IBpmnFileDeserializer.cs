using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test;

public interface IBpmnFileDeserializer
{
    BpmnDefinitions Deserialize(string filePath);
}